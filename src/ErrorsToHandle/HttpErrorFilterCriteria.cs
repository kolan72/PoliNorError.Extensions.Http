namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents the base class for criteria used to filter HTTP errors.
	/// </summary>
	public abstract class HttpErrorFilterCriteria
	{
		internal bool IncludeHttpRequestException { get; set; }

		internal abstract bool Contains(int statusCode);
	}
}
