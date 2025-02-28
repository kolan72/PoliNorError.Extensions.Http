using PoliNorError.Extensions.Http;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
	public class AskNamedCatService : IAskCatService
	{
		private readonly IHttpClientFactory _factory;

		public AskNamedCatService(IHttpClientFactory factory)
		{
			_factory = factory;
		}

		public async Task<CatAnswer> GetCatFactAsync(CancellationToken token = default)
		{
			try
			{
				token.ThrowIfCancellationRequested();
				var client = _factory.CreateClient(HttpClientConfiguration.CatClientHttpClientName);

				using var response = await client.GetAsync(CatFactUriMutator.GetCatFactUri(), token).ConfigureAwait(false);
				
				var contentString = await response.Content.ReadAsStringAsync(token);
				var catResponse = JsonSerializer.Deserialize<CatResponse>(contentString);

				return new CatAnswer(catResponse.Fact);
			}
			catch (OperationCanceledException oe)
			{
				return CatAnswer.FromError(oe);
			}
			catch (HttpPolicyResultException hpre)
			{
				return CatAnswer.FromError(hpre);
			}
			catch (Exception ex)
			{
				return CatAnswer.FromError(ex);
			}
		}
	}
}
