﻿using System.Net;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	public static class HttpErrorFilter
	{
		/// <summary>
		/// Creates <see cref="ConfigurableHttpErrorFilter"/> that handles <see cref="HttpRequestException"/>.
		/// </summary>
		/// <returns></returns>
		public static ConfigurableHttpErrorFilter HandleHttpRequestException() => ConfigurableHttpErrorFilter.CreateWithHttpRequestException();

		/// <summary>
		/// Filters status codes that are not in the range 200-299.
		/// </summary>
		/// <returns></returns>
		public static NonSuccessfulStatusCodes HandleNonSuccessfulStatusCodes() => new NonSuccessfulStatusCodes();

		/// <summary>
		/// Creates a <see cref="ConfigurableHttpErrorFilter"/> that does not handle any http errors.
		/// </summary>
		/// <returns></returns>
		public static EmptyHttpErrorFilter None() => new EmptyHttpErrorFilter();

		/// <summary>
		/// Filters <paramref name="statusCode"/> status code.
		/// </summary>
		/// <param name="statusCode"><see cref="HttpStatusCode"/> to filter.</param>
		/// <returns></returns>
		public static ConfigurableHttpErrorFilter HandleStatusCode(HttpStatusCode statusCode) => new ConfigurableHttpErrorFilter(StatusCodesToHandle.HandleStatusCode(statusCode));

		/// <summary>
		/// Filters <paramref name="statusCode"/> status code.
		/// </summary>
		/// <param name="statusCode">Status code to filter.</param>
		/// <returns></returns>
		public static ConfigurableHttpErrorFilter HandleStatusCode(int statusCode) => new ConfigurableHttpErrorFilter(StatusCodesToHandle.HandleStatusCode(statusCode));

		/// <summary>
		/// Creates <see cref="ConfigurableHttpErrorFilter"/> that handles <see cref="HttpStatusCode"/>> from <see cref="StatusCodesCategory"/>.
		/// </summary>
		/// <param name="statusCodesCategory"></param>
		/// <returns></returns>
		public static ConfigurableHttpErrorFilter HandleStatusCodeCategory(StatusCodesCategory statusCodesCategory)
		{
			var statusCodesToHandle = StatusCodesToHandle.HandleStatusCodeCategory(statusCodesCategory);
			return new ConfigurableHttpErrorFilter(statusCodesToHandle);
		}

		/// <summary>
		/// Creates <see cref="ConfigurableHttpErrorFilter"/> that handles
		/// <list type="bullet">
		/// <item><description>any status code 500 or above.</description></item>
		/// <item><description>429 (Too Many Requests).</description></item>
		/// <item><description>408 (Request Timeout).</description></item>
		/// <item><description>Network failures (System.Net.Http.HttpRequestException).</description></item>
		/// </list>
		/// </summary>
		/// <returns>HttpErrorsToHandle</returns>
		public static ConfigurableHttpErrorFilter HandleTransientHttpErrors()
		{
			return new ConfigurableHttpErrorFilter(StatusCodesToHandle.HandleTransientHttpStatusCodes()){ IncludeHttpRequestException = true };
		}

		/// <summary>
		/// Creates <see cref="ConfigurableHttpErrorFilter"/> that handles
		/// <list type="bullet">
		/// <item><description>any status code 500 or above.</description></item>
		/// <item><description>429 (Too Many Requests).</description></item>
		/// <item><description>408 (Request Timeout).</description></item>
		/// </list>
		/// </summary>
		/// <returns>HttpErrorsToHandle</returns>
		public static ConfigurableHttpErrorFilter HandleTransientHttpStatusCodes()
		{
			return new ConfigurableHttpErrorFilter(StatusCodesToHandle.HandleTransientHttpStatusCodes());
		}
	}
}
