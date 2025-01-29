namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents all non-successful status code categories.
	/// </summary>
	public enum StatusCodesCategory
	{
		/// <summary>
		/// Informational
		/// </summary>
		Status1XX = 1,
		/// <summary>
		/// Redirection
		/// </summary>
		Status3XX = 3,
		/// <summary>
		/// Client error
		/// </summary>
		Status4XX = 4,
		/// <summary>
		/// Server error
		/// </summary>
		Status5XX = 5
	}
}
