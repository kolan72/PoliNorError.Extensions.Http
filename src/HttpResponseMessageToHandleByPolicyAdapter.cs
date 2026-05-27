using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	internal static class HttpResponseMessageToHandleByPolicyAdapter
	{
		internal static async Task<HttpResponseMessage> AdaptAsync(HttpResponseMessage result, HttpErrorFilterCriteria statusCodesStore)
		{
			// Early return for the success path — avoids one level of nesting
			if (!statusCodesStore.Contains((int)result.StatusCode))
				return result;

			var failedResponse = new FailedHttpResponse
			{
				// ConfigureAwait(false) is required here: this runs inside a DelegatingHandler
				// which may be called from a synchronization-context-bound host (e.g. ASP.NET Framework).
				// Omitting it risks a deadlock when the context thread is blocked waiting for this task.
				Content           = !(result.Content is null) ? await result.Content.ReadAsStringAsync().ConfigureAwait(false) : null,
				ContentEncoding   = result.Content?.Headers.ContentEncoding ?? Array.Empty<string>(),
				Version           = result.RequestMessage?.Version,
				ContentLength     = result.Content?.Headers.ContentLength,
				ContentType       = result.Content?.Headers.ContentType?.MediaType,
				ResponseUri       = result.RequestMessage?.RequestUri,
				Server            = result.Headers.Server.ToString(),
				StatusCode        = result.StatusCode,
				ResponseHeaders   = result.Headers,
				ContentHeaders    = result.Content?.Headers,
				StatusDescription = result.ReasonPhrase
			};

			throw new FailedHttpResponseException(failedResponse);
		}
	}
}
