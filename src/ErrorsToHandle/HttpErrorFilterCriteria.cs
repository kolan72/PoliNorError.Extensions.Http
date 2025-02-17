namespace PoliNorError.Extensions.Http
{
	public abstract class HttpErrorFilterCriteria
	{
		internal bool IncludeHttpRequestException { get; set; }

		internal abstract bool Contains(int statusCode);
	}
}
