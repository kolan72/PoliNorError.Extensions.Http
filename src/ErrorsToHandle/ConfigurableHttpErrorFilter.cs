using System.Net;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	///<inheritdoc cref="IHttpErrorFilter"/>
	public class ConfigurableHttpErrorFilter : HttpErrorFilterCriteria
	{
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

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		public bool Contains(HttpStatusCode statusCode) => _statusCodesToHandle.Contains(statusCode);

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		internal override bool Contains(int statusCode) => _statusCodesToHandle.Contains(statusCode);

		private ConfigurableHttpErrorFilter AddStatusCodeCategory(StatusCodesCategory statusCategoryNumber)
		{
			_statusCodesToHandle.AddStatusCodeCategory(statusCategoryNumber);
			return this;
		}
	}
}
