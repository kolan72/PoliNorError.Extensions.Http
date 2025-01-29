using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	internal static class HttpResponseMessageToHandleByPolicyAdapter
	{
		internal static async Task<HttpResponseMessage> AdaptAsync(HttpResponseMessage result, IHttpErrorFilter statusCodesStore)
		{
			if (statusCodesStore.Contains((int)result.StatusCode))
			{
				var failedResponse = new FailedHttpResponse()
				{
					Content = !(result.Content is null) ? await result.Content.ReadAsStringAsync() : null,
					ContentEncoding = result.Content?.Headers.ContentEncoding ?? Array.Empty<string>(),
					Version = result.RequestMessage?.Version,
					ContentLength = result.Content?.Headers.ContentLength,
					ContentType = result.Content?.Headers.ContentType?.MediaType,
					ResponseUri = result.RequestMessage?.RequestUri,
					Server = result.Headers.Server.ToString(),
					StatusCode = result.StatusCode,
					ResponseHeaders = result.Headers,
					ContentHeaders = result.Content?.Headers,
					StatusDescription = result.ReasonPhrase
				};

				throw new FailedHttpResponseException(failedResponse);
			}
			else
			{
				return result;
			}
		}
	}
}
