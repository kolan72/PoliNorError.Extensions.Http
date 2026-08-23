using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Helper methods and attribute-name constants for OpenTelemetry HTTP
	/// semantic-convention tags.
	/// </summary>
	/// <remarks>
	/// The attribute names defined here mirror the OpenTelemetry semantic-conventions
	/// registry for HTTP spans (stable conventions):
	/// https://opentelemetry.io/docs/specs/semconv/http/http-spans/
	/// </remarks>
	internal static class HttpSemanticConventions
	{
		/// <summary>
		/// Name of the tag carrying the HTTP request method (e.g. <c>"GET"</c>, <c>"POST"</c>).
		/// Mirrors the <c>http.request.method</c> attribute.
		/// </summary>
		public const string HttpRequestMethodTag = "http.request.method";

		/// <summary>
		/// Name of the tag carrying the HTTP response status code as an <see cref="int"/>
		/// (e.g. <c>200</c>, <c>503</c>). Mirrors the <c>http.response.status_code</c> attribute.
		/// </summary>
		public const string HttpResponseStatusCodeTag = "http.response.status_code";

		/// <summary>
		/// Name of the tag carrying the absolute request URL. Sensitive query-string
		/// values are redacted. Mirrors the <c>url.full</c> attribute.
		/// </summary>
		public const string UrlFullTag = "url.full";

		/// <summary>
		/// Name of the tag carrying the server domain name or IP address.
		/// Mirrors the <c>server.address</c> attribute.
		/// </summary>
		public const string ServerAddressTag = "server.address";

		/// <summary>
		/// Name of the tag carrying the server port number. Mirrors the
		/// <c>server.port</c> attribute. Emitted only when <see cref="ServerAddressTag"/>
		/// is set and the port is non-default for the request scheme.
		/// </summary>
		public const string ServerPortTag = "server.port";

		/// <summary>
		/// Name of the tag carrying the request path. Mirrors the <c>url.path</c> attribute.
		/// </summary>
		public const string UrlPathTag = "url.path";

		// Query-parameter names whose values MUST be redacted in url.full, per the
		// OpenTelemetry specification (url.full note [4], url.query note [10]):
		//   https://opentelemetry.io/docs/specs/semconv/http/http-spans/
		//
		// The OTel default list is:
		//   X-Amz-Signature, X-Amz-Credential, X-Amz-Security-Token, sig, X-Goog-Signature
		//
		// The spec states: "Matching of query parameter keys against the sensitive
		// list SHOULD be case-sensitive." Per RFC 3986, query parameter names are
		// inherently case-sensitive, so StringComparer.Ordinal preserves that semantics.
		//
		// This set extends the OTel defaults with additional well-known sensitive
		// parameter names commonly seen in cloud-provider and OAuth/OIDC query strings.
		private static readonly HashSet<string> SensitiveQueryParameters = new HashSet<string>(StringComparer.Ordinal)
		{
			// --- Cloud Provider Signatures (OTel defaults + extensions) ---
			"X-Amz-Signature",
			"X-Amz-Credential",
			"X-Amz-Security-Token",
			"sig",
			"X-Goog-Signature",
			"X-Goog-Credential",      // GCP equivalent of X-Amz-Credential
			"AWSAccessKeyId",         // AWS query API access key ID
			"Signature",              // Generic signature parameter (AWS SigV4, etc.)

			// --- OAuth / OIDC ---
			"code",                   // OAuth 2.0 Authorization Code
			"access_token",           // OAuth 2.0 access token
			"refresh_token",          // OAuth 2.0 refresh token
			"id_token",               // OIDC ID token
			"client_secret",          // OAuth 2.0 client secret

			// --- Generic Application Auth ---
			"token",                  // Generic bearer token
			"api_key",                // API key (snake_case)
			"apikey",                 // API key (no separator)
			"password",               // Plain-text password
			"pwd",                    // Password shorthand
			"jwt",                    // Raw JWT in query string
		};

		/// <summary>
		/// Tags <paramref name="activity"/> with HTTP request semantic-convention attributes
		/// derived from <paramref name="request"/>.
		/// </summary>
		/// <remarks>
		/// Safe to call when no listener is attached — <paramref name="activity"/> may be
		/// <c>null</c> and is silently ignored, preserving the zero-cost
		/// no-listener guarantee.
		/// </remarks>
		public static void SetRequestTags(Activity activity, HttpRequestMessage request)
		{
			if (activity is null || request is null)
				return;

			activity.SetTag(HttpRequestMethodTag, request.Method?.Method);

			var uri = request.RequestUri;
			if (uri is null)
				return;

			if (!string.IsNullOrEmpty(uri.Host))
			{
				activity.SetTag(ServerAddressTag, uri.Host);

				var port = uri.Port;
				if (port > 0 && !IsDefaultPort(uri.Scheme, port))
					activity.SetTag(ServerPortTag, port);
			}

			activity.SetTag(UrlPathTag, uri.AbsolutePath);

			activity.SetTag(UrlFullTag, BuildSanitizedUrl(uri));
		}

		/// <summary>
		/// Tags <paramref name="activity"/> with the <c>http.response.status_code</c> attribute
		/// as an <see cref="int"/>.
		/// </summary>
		public static void SetResponseStatusCodeTag(Activity activity, HttpStatusCode statusCode)
		{
			if (activity is null)
				return;
			activity.SetTag(HttpResponseStatusCodeTag, (int)statusCode);
		}

		/// <summary>
		/// Sets the activity status from an HTTP response status code, applying the
		/// OpenTelemetry semantic conventions for CLIENT spans:
		/// 4xx SHOULD be Error, 5xx MUST be Error; 1xx–3xx are Ok.
		/// The status description is intentionally left blank — the reason can be
		/// inferred from the <c>http.response.status_code</c> tag.
		/// </summary>
		public static void SetActivityStatusFromHttpStatusCode(Activity activity, HttpStatusCode statusCode)
		{
			if (activity is null)
				return;
			activity.SetStatus((int)statusCode >= 400 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
		}

		/// <summary>
		/// Builds a sanitized <c>url.full</c> value: strips userinfo (credentials)
		/// and redacts sensitive query-parameter values, replacing them with
		/// <c>REDACTED</c> while preserving the parameter name.
		/// </summary>
		private static string BuildSanitizedUrl(Uri uri)
		{
			if (string.IsNullOrEmpty(uri.Query))
				return uri.ToString();

			var sanitizedQuery = RedactSensitiveQueryParams(uri.Query);

			var builder = new UriBuilder(uri)
			{
				Query = sanitizedQuery,
				UserName = string.Empty,
				Password = string.Empty
			};

			if (uri.UserInfo != null)
			{
				return builder.Uri.ToString();
			}

			return uri.ToString().Split('?')[0] + sanitizedQuery;
		}

		/// <summary>
		/// Replaces the values of known sensitive query parameters with <c>REDACTED</c>,
		/// preserving non-sensitive parameters and the overall query structure.
		/// </summary>
		private static string RedactSensitiveQueryParams(string rawQuery)
		{
			if (string.IsNullOrEmpty(rawQuery))
				return rawQuery;

			// rawQuery includes the leading '?' — strip it for processing
			var query = rawQuery.StartsWith("?", StringComparison.Ordinal)
				? rawQuery.Substring(1)
				: rawQuery;

			if (string.IsNullOrEmpty(query))
				return rawQuery;

			var pairs = query.Split('&');
			for (int i = 0; i < pairs.Length; i++)
			{
				var pair = pairs[i];
				var eqIndex = pair.IndexOf('=');
				if (eqIndex < 0)
				{
					// key with no value — check if sensitive
					if (IsSensitive(pair))
						pairs[i] = pair + "=REDACTED";
				}
				else
				{
					var key = pair.Substring(0, eqIndex);
					if (IsSensitive(key))
						pairs[i] = key + "=REDACTED";
				}
			}

			return "?" + string.Join("&", pairs);
		}

		private static bool IsSensitive(string key) => SensitiveQueryParameters.Contains(key);

		/// <summary>
		/// Returns <c>true</c> when <paramref name="port"/> is the default port
		/// for the given URI <paramref name="scheme"/> (80 for <c>http</c>,
		/// 443 for <c>https</c>). Default ports are omitted per OTel convention.
		/// </summary>
		private static bool IsDefaultPort(string scheme, int port)
		{
			return (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && port == 80) ||
			       (scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && port == 443);
		}
	}
}
