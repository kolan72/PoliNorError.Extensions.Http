using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
	public class HandlerThatMakesTransientErrorFrom404 : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if (res.StatusCode == HttpStatusCode.NotFound)
			{
				var i = Utils.Randomizer.Next();

				res.StatusCode = i switch
				{
					1 => HttpStatusCode.RequestTimeout,
					2 => HttpStatusCode.TooManyRequests,
					3 => HttpStatusCode.BadGateway,
					_ => throw new HttpRequestException()
				};
			}
			return res;
		}
	}
}
