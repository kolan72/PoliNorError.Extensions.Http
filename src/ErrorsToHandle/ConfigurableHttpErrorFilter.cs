using System.Net;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents the configurable filter for setting http errors that should be handled in a resilient manner.
	/// </summary>
	public class ConfigurableHttpErrorFilter : HttpErrorFilterCriteria
	{
		internal ConfigurableHttpErrorFilter(){}

		internal StatusCodesToHandle _statusCodesToHandle = new StatusCodesToHandle();

		public ConfigurableHttpErrorFilter OrInformational() => OrStatusCodeCategory(StatusCodesCategory.Status1XX);
		public ConfigurableHttpErrorFilter OrRedirection() => OrStatusCodeCategory(StatusCodesCategory.Status3XX);

		public ConfigurableHttpErrorFilter OrClientError() => OrStatusCodeCategory(StatusCodesCategory.Status4XX);
		public ConfigurableHttpErrorFilter OrServerError() => OrStatusCodeCategory(StatusCodesCategory.Status5XX);

		/// <summary>
		/// Adds the <paramref name="statusCodesCategory"/> to the handling filter.
		/// </summary>
		/// <param name="statusCodesCategory">Status code category to filter.</param>
		/// <returns></returns>
		public ConfigurableHttpErrorFilter OrStatusCodeCategory(StatusCodesCategory statusCodesCategory) => AddStatusCodeCategory(statusCodesCategory);

		/// <summary>
		/// Adds transient <see cref="HttpStatusCode"/>s (429, 408 any status code 500 or above) to the handling filter.
		/// </summary>
		/// <returns></returns>
		public ConfigurableHttpErrorFilter OrTransientHttpStatusCodes()
		{
			_statusCodesToHandle.OrTransientHttpStatusCodes();
			return this;
		}

		/// <summary>
		/// Adds the <see cref="HttpRequestException"/> to the handling filter.
		/// </summary>
		/// <returns></returns>
		public ConfigurableHttpErrorFilter OrHttpRequestException()
		{
			IncludeHttpRequestException = true;
			return this;
		}

		/// <summary>
		///  Adds the <paramref name="statusCode"/> to the handling filter.
		/// </summary>
		/// <param name="statusCode">Status code to filter.</param>
		/// <returns></returns>
		public ConfigurableHttpErrorFilter OrStatusCode(HttpStatusCode statusCode)
		{
			_statusCodesToHandle.OrStatusCode((int)statusCode);
			return this;
		}

		/// <summary>
		/// Adds the <paramref name="statusCode"/> to the handling filter.
		/// </summary>
		/// <param name="statusCode">Status code to filter.</param>
		/// <returns></returns>
		public ConfigurableHttpErrorFilter OrStatusCode(int statusCode)
		{
			_statusCodesToHandle.OrStatusCode(statusCode);
			return this;
		}

		/// <summary>
		/// Determines whether an <paramref name="statusCode"/> is in the filter.
		/// </summary>
		/// <param name="statusCode">Status code to check.</param>
		/// <returns></returns>
		internal bool Contains(HttpStatusCode statusCode) => _statusCodesToHandle.Contains(statusCode);

		internal override bool Contains(int statusCode) => _statusCodesToHandle.Contains(statusCode);

		private ConfigurableHttpErrorFilter AddStatusCodeCategory(StatusCodesCategory statusCategoryNumber)
		{
			_statusCodesToHandle.AddStatusCodeCategory(statusCategoryNumber);
			return this;
		}
	}
}
