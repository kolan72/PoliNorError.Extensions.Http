using System.Net;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	///<inheritdoc cref="IHttpErrorFilter"/>
	public class HttpErrorFilter : IHttpErrorFilter
	{
		private StatusCodesToHandle _statusCodesToHandle = new StatusCodesToHandle();

		/// <summary>
		/// Creates <see cref="HttpErrorFilter"/> that handles <see cref="HttpRequestException"/>.
		/// </summary>
		/// <returns></returns>
		public static HttpErrorFilter HandleHttpRequestException() => new HttpErrorFilter() { IncludeHttpRequestException  = true};

		/// <summary>
		/// Filters status codes that are not in the range 200-299.
		/// </summary>
		/// <returns></returns>
		public static AllNonSuccessfulCodesErrors HandleAllNonSuccessStatusCodes() => new AllNonSuccessfulCodesErrors();

		/// <summary>
		/// Creates a <see cref="HttpErrorFilter"/> that does not handle any http errors.
		/// </summary>
		/// <returns></returns>
		public static EmptyHttpErrorFilter HandleNone() => new EmptyHttpErrorFilter();

		/// <summary>
		/// Filters <paramref name="statusCode"/> status code.
		/// </summary>
		/// <param name="statusCode"><see cref="HttpStatusCode"/> to filter.</param>
		/// <returns></returns>
		public static HttpErrorFilter HandleStatusCode(HttpStatusCode statusCode) => new HttpErrorFilter() {_statusCodesToHandle = StatusCodesToHandle.HandleStatusCode(statusCode) };

		/// <summary>
		/// Filters <paramref name="statusCode"/> status code.
		/// </summary>
		/// <param name="statusCode">Status code to filter.</param>
		/// <returns></returns>
		public static HttpErrorFilter HandleStatusCode(int statusCode) => new HttpErrorFilter() { _statusCodesToHandle = StatusCodesToHandle.HandleStatusCode(statusCode) };

		/// <summary>
		/// Creates <see cref="HttpErrorFilter"/> that handles <see cref="HttpStatusCode"/>> from <see cref="StatusCodesCategory"/>.
		/// </summary>
		/// <param name="statusCodesCategory"></param>
		/// <returns></returns>
		public static HttpErrorFilter HandleStatusCodeCategory(StatusCodesCategory statusCodesCategory)
		{
			var statusCodesToHandle = StatusCodesToHandle.HandleStatusCodeCategory(statusCodesCategory);
			return new HttpErrorFilter() { _statusCodesToHandle = statusCodesToHandle };
		}

		/// <summary>
		/// Creates <see cref="HttpErrorFilter"/> that handles
		/// <list type="bullet">
		/// <item><description>any status code 500 or above.</description></item>
		/// <item><description>429 (Too Many Requests).</description></item>
		/// <item><description>408 (Request Timeout).</description></item>
		/// <item><description>Network failures (System.Net.Http.HttpRequestException).</description></item>
		/// </list>
		/// </summary>
		/// <returns>HttpErrorsToHandle</returns>
		public static HttpErrorFilter HandleTransientHttpErrors()
		{
			return new HttpErrorFilter() { _statusCodesToHandle = StatusCodesToHandle.HandleTransientHttpStatusCodes(), IncludeHttpRequestException = true };
		}

		/// <summary>
		/// Creates <see cref="HttpErrorFilter"/> that handles
		/// <list type="bullet">
		/// <item><description>any status code 500 or above.</description></item>
		/// <item><description>429 (Too Many Requests).</description></item>
		/// <item><description>408 (Request Timeout).</description></item>
		/// </list>
		/// </summary>
		/// <returns>HttpErrorsToHandle</returns>
		public static HttpErrorFilter HandleTransientHttpStatusCodes()
		{
			return new HttpErrorFilter() { _statusCodesToHandle = StatusCodesToHandle.HandleTransientHttpStatusCodes()};
		}

		public HttpErrorFilter OrInformational() => OrStatusCodeCategory(StatusCodesCategory.Status1XX);
		public HttpErrorFilter OrRedirection() => OrStatusCodeCategory(StatusCodesCategory.Status3XX);

		public HttpErrorFilter OrClientError() => OrStatusCodeCategory(StatusCodesCategory.Status4XX);
		public HttpErrorFilter OrServerError() => OrStatusCodeCategory(StatusCodesCategory.Status5XX);

		/// <summary>
		/// Adds the <paramref name="statusCodesCategory"/> to the handling filter.
		/// </summary>
		/// <param name="statusCodesCategory">Status code category to filter.</param>
		/// <returns></returns>
		public HttpErrorFilter OrStatusCodeCategory(StatusCodesCategory statusCodesCategory) => AddStatusCodeCategory(statusCodesCategory);

		/// <summary>
		/// Adds transient <see cref="HttpStatusCode"/>s (429, 408 any status code 500 or above) to the handling filter.
		/// </summary>
		/// <returns></returns>
		public HttpErrorFilter OrTransientHttpStatusCodes()
		{
			_statusCodesToHandle.OrTransientHttpStatusCodes();
			return this;
		}

		/// <summary>
		/// Adds the <see cref="HttpRequestException"/> to the handling filter.
		/// </summary>
		/// <returns></returns>
		public HttpErrorFilter OrHttpRequestException()
		{
			IncludeHttpRequestException = true;
			return this;
		}

		/// <summary>
		///  Adds the <paramref name="statusCode"/> to the handling filter.
		/// </summary>
		/// <param name="statusCode">Status code to filter.</param>
		/// <returns></returns>
		public HttpErrorFilter OrStatusCode(HttpStatusCode statusCode)
		{
			_statusCodesToHandle.OrStatusCode((int)statusCode);
			return this;
		}

		/// <summary>
		/// Adds the <paramref name="statusCode"/> to the handling filter.
		/// </summary>
		/// <param name="statusCode">Status code to filter.</param>
		/// <returns></returns>
		public HttpErrorFilter OrStatusCode(int statusCode)
		{
			_statusCodesToHandle.OrStatusCode(statusCode);
			return this;
		}

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		public bool Contains(HttpStatusCode statusCode) => _statusCodesToHandle.Contains(statusCode);

		///<inheritdoc cref="IHttpErrorFilter.Contains(int)"/>
		public bool Contains(int statusCode) => _statusCodesToHandle.Contains(statusCode);

		///<inheritdoc cref="IHttpErrorFilter.IncludeHttpRequestException"/>
		public bool IncludeHttpRequestException { get; private set; }

		private HttpErrorFilter AddStatusCodeCategory(StatusCodesCategory statusCategoryNumber)
		{
			_statusCodesToHandle.AddStatusCodeCategory(statusCategoryNumber);
			return this;
		}
	}
}
