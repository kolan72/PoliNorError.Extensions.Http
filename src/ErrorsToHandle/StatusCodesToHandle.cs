using System;
using System.Collections.Generic;
using System.Net;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Contains status codes that will be interpreted as an error that policy should handle.
	/// </summary>
	internal sealed class StatusCodesToHandle
	{
		private readonly HashSet<int> _categoriesSet = new HashSet<int>();
		private readonly HashSet<int> _statusCodesSet = new HashSet<int>();

		public static StatusCodesToHandle HandleStatusCode(HttpStatusCode statusCode) => HandleStatusCode((int)statusCode);
		public static StatusCodesToHandle HandleStatusCode(int statusCode) => new StatusCodesToHandle().OrStatusCode(statusCode);

		public static StatusCodesToHandle HandleStatusCodeCategory(StatusCodesCategory statusCodesCategory) => new StatusCodesToHandle().AddStatusCodeCategory(statusCodesCategory);

		public static StatusCodesToHandle HandleTransientHttpStatusCodes() => new StatusCodesToHandle().OrTransientHttpStatusCodes();

		public StatusCodesToHandle OrInformational() => AddStatusCodeCategory(StatusCodesCategory.Status1XX);

		public StatusCodesToHandle OrRedirection() => AddStatusCodeCategory(StatusCodesCategory.Status3XX);

		public StatusCodesToHandle OrClientError() => AddStatusCodeCategory(StatusCodesCategory.Status4XX);

		public StatusCodesToHandle OrServerError() => AddStatusCodeCategory(StatusCodesCategory.Status5XX);

		public StatusCodesToHandle OrTransientHttpStatusCodes() => AddStatusCodeCategory(StatusCodesCategory.Status5XX).OrStatusCode(HttpStatusCode.RequestTimeout).OrStatusCode(429);

		public StatusCodesToHandle OrStatusCode(HttpStatusCode statusCode)
		{
			return OrStatusCode((int)statusCode);
		}

		public StatusCodesToHandle OrStatusCode(int statusCode)
		{
			if (!IsStatusCodeValid(statusCode))
			{
				throw new ArgumentException($"The HTTP status code {statusCode} is not valid.");
			}
			_statusCodesSet.Add(statusCode);
			return this;
		}

		public bool Contains(int statusCode)
		{
			if (!IsStatusCodeValid(statusCode))
				return false;

			var categorySetKey = statusCode / 100;

			if (SuccessfulStatusCodesChecker.IsSuccessStatusCodeCategory(categorySetKey))
				return false;

			if (_categoriesSet.Contains(categorySetKey))
				return true;

			return _statusCodesSet.Contains(statusCode);
		}

		public bool Contains(HttpStatusCode statusCode)
		{
			return Contains((int)statusCode);
		}

		internal StatusCodesToHandle AddStatusCodeCategory(StatusCodesCategory statusCategoryNumber)
		{
			_categoriesSet.Add((int)statusCategoryNumber);
			return this;
		}

		private static bool IsStatusCodeValid(int statusCode)
		{
			return statusCode >= 100 && statusCode <= 599;
		}
	}
}
