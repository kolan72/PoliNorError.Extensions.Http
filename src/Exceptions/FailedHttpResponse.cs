using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Stores failed http response data.
	/// </summary>
	///<remarks>
	/// Based on RestSharp RestResponseBase class.
	/// https://github.com/restsharp/RestSharp/blob/dev/src/RestSharp/Response/RestResponseBase.cs
	///</remarks>
	public class FailedHttpResponse
	{
		/// <summary>
		/// MIME content type of response
		/// </summary>
		public string ContentType { get; set; }

		/// <summary>
		/// Length in bytes of the response content
		/// </summary>
		public long? ContentLength { get; set; }

		/// <summary>
		/// Encoding of the response content
		/// </summary>
		public ICollection<string> ContentEncoding { get; set; } = Array.Empty<string>();

		/// <summary>
		/// String representation of response content
		/// </summary>
		public string Content { get; set; }

		/// <summary>
		/// HTTP response status code
		/// </summary>
		public HttpStatusCode StatusCode { get; set; }

		/// <summary>
		/// Description of HTTP status returned
		/// </summary>
		public string StatusDescription { get; set; }

		/// <summary>
		/// The URL that actually responded to the content (different from request if redirected)
		/// </summary>
		public Uri ResponseUri { get; set; }

		/// <summary>
		/// HttpResponseMessage.Headers
		/// </summary>
		public HttpResponseHeaders ResponseHeaders { get; set; }

		/// <summary>
		/// HttpResponseMessage.Content?.Headers
		/// </summary>
		public HttpContentHeaders ContentHeaders { get; set; }

		/// <summary>
		/// HttpWebResponse.Server
		/// </summary>
		public string Server { get; set; }

		/// <summary>
		/// HTTP protocol version of the request
		/// </summary>
		public Version Version { get; set; }
	}
}
