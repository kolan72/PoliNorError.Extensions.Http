using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal static class ServiceCollectionExtensions
	{
		public static IHttpClientBuilder AddFakeHttpClient(this ServiceCollection services)
		{
			services.AddTransient<FailingHandler>();
			return services.AddHttpClient("my-httpclient", client => client.BaseAddress = new Uri("http://any.localhost"))
				.ConfigurePrimaryHttpMessageHandler<FailingHandler>();
		}

		public static IHttpClientBuilder AddFakeHttpClientWithRetryHeader(this ServiceCollection services)
		{
			var httpMessageHandlerMock = new MockHttpMessageHandler();
			httpMessageHandlerMock
				.When(HttpMethod.Get, "http://any.localhost/any")
				.Respond(
				async () =>
				{ await Task.Delay(1);
					var response = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
					response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
					return response;
				}
				);

			return services.AddHttpClient("httpclient-with-retryheader").ConfigurePrimaryHttpMessageHandler(() => httpMessageHandlerMock);
		}

		public static IHttpClientBuilder AddFakeSussessHttpClient(this ServiceCollection services)
		{
			var httpMessageHandlerMock = new MockHttpMessageHandler();
			_ = httpMessageHandlerMock
				.When(HttpMethod.Get, "http://any.localhost/any")
				.Respond(System.Net.HttpStatusCode.OK, "application/json", "{'name' : 'Test McGee'}");

			return services.AddHttpClient("my-httpclient").ConfigurePrimaryHttpMessageHandler(() => httpMessageHandlerMock);
		}
	}

	/// <summary>
	/// DelegatingHandler that always throws HttpRequestException - used for fast retry tests without real HTTP calls.
	/// </summary>
	internal class FailingHandler : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await Task.Delay(1, cancellationToken);
			throw new HttpRequestException("Simulated network failure");
		}
	}
}
