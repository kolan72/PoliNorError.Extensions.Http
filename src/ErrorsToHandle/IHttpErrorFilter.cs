using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents the filter to set http errors that should be handled in a resilient manner.
	/// </summary>
	public interface IHttpErrorFilter
	{
		/// <summary>
		/// Indicates whether a <see cref="HttpRequestException"/> exception should be handled.
		/// </summary>
		bool IncludeHttpRequestException { get; }

		/// <summary>
		/// Determines whether an <paramref name="statusCode"/> is in the filter.
		/// </summary>
		/// <param name="statusCode">Status code to check.</param>
		/// <returns></returns>
		bool Contains(int statusCode);
	}
}
