namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This class is primarily for internal use.
	/// </summary>
	public class EmptyHttpErrorFilter : IEmptyHttpErrorFilter
	{
		internal EmptyHttpErrorFilter(){}

		///<inheritdoc cref="IHttpErrorFilter.IncludeHttpRequestException"/>
		public bool IncludeHttpRequestException => false;

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		public bool Contains(int statusCode) => false;
	}

	/// <summary>
	/// Interface for not filtering any http errors at all.
	/// </summary>
	public interface IEmptyHttpErrorFilter : IHttpErrorFilter
	{}
}
