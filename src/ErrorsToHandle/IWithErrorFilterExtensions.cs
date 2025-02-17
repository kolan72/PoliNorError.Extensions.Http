using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	internal static class WithErrorFilterExtensions
	{
		internal static T WithErrorsFilter<T>(this T withErrorFilter, HttpErrorFilterCriteria errorsToHandle) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
		{
			if (errorsToHandle is IEmptyHttpErrorFilter)
			{
				withErrorFilter.ExcludeError<HttpRequestException>().ExcludeError<FailedHttpResponseException>();
				return withErrorFilter;
			}
			withErrorFilter.IncludeError<FailedHttpResponseException>();
			if (errorsToHandle.IncludeHttpRequestException)
			{
				withErrorFilter.IncludeError<HttpRequestException>();
			}

			return withErrorFilter;
		}
	}
}
