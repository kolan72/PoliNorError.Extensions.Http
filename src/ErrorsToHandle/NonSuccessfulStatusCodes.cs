namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This class is primarily for internal use.
	/// </summary>
	public class NonSuccessfulStatusCodes : HttpErrorFilterCriteria
	{
        internal NonSuccessfulStatusCodes(){}

		internal override bool Contains(int statusCode) => !SuccessfulStatusCodesChecker.IsSuccessStatusCode(statusCode);
	}
}
