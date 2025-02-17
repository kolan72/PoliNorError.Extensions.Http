namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This class is primarily for internal use.
	/// </summary>
	public class EmptyHttpErrorFilter : HttpErrorFilterCriteria, IEmptyHttpErrorFilter
	{
		internal EmptyHttpErrorFilter(){}

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		internal override bool Contains(int statusCode) => false;
	}

	/// <summary>
	/// Interface for not filtering any http errors at all.
	/// </summary>
	public interface IEmptyHttpErrorFilter
	{}
}
