using PoliNorError.Extensions.Http;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
	public class AskCatService : IAskCatService
	{
		private readonly HttpClient _client;

		public AskCatService(HttpClient client)
		{
			_client = client;
		}

		public async Task<CatAnswer> GetCatFactAsync(CancellationToken token = default)
		{
			try
			{
				token.ThrowIfCancellationRequested();

				using var response = await _client.GetAsync(CatFactUriMutator.GetCatFactUri(), token);

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
