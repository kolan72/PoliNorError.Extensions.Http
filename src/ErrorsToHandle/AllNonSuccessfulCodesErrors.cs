namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This class is primarily for internal use.
	/// </summary>
	public class AllNonSuccessfulCodesErrors : IHttpErrorFilter
	{
        internal AllNonSuccessfulCodesErrors(){}

		///<inheritdoc cref="IHttpErrorFilter.IncludeHttpRequestException"/>
		public bool IncludeHttpRequestException { get; internal set; }

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		public bool Contains(int statusCode) => !SuccessfulStatusCodesChecker.IsSuccessStatusCode(statusCode);
	}
}
