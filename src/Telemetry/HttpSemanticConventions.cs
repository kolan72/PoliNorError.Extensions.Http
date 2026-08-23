using System;
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
		/// Name of the tag carrying the request path. Mirrors the <c>url.path</c> attribute.
		/// </summary>
		public const string UrlPathTag = "url.path";

		// Query-parameter names whose values MUST be redacted in url.full, per the
		// OpenTelemetry specification. Matching is case-sensitive.
		private static readonly string[] SensitiveQueryParameters =
		{
			"X-Amz-Signature",
			"X-Amz-Credential",
			"X-Amz-Security-Token",
			"sig",
			"X-Goog-Signature"
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
				activity.SetTag(ServerAddressTag, uri.Host);

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

		private static bool IsSensitive(string key)
		{
			foreach (var sensitive in SensitiveQueryParameters)
			{
				if (key.Equals(sensitive, StringComparison.Ordinal))
					return true;
			}
			return false;
		}
	}
}
