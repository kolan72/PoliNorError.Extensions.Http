using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http;
using System.Threading;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		public void Should_NoHttpErrorsToHandle_Filters_Correctly_For_HttpRequestException()
		{
			int i = 0;

			var services = new ServiceCollection();

			services
				.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleNone()));

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.HasFailedResponse, Is.False);
				Assert.That(exception.IsErrorExpected, Is.False);
				Assert.That(i, Is.EqualTo(0));
			}
		}

		[Test]
		public void Should_PreCanceling_Set_IsCanceled_To_True()
		{
			int i = 0;

			using (var cts = new CancellationTokenSource())
			{
				cts.Cancel();
				var services = new ServiceCollection();

				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
															//No filter works with precanceling, so we do not set an http filter.
															.AsFinalHandler(HttpErrorFilter.HandleNone())
															);

				var serviceProvider = services.BuildServiceProvider();

				using (var scope = serviceProvider.CreateScope())
				{
					var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
					var request = new HttpRequestMessage(HttpMethod.Get, "/any");

					var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request, cts.Token));
					Assert.That(exception != null && exception.IsCanceled, Is.True);

					Assert.That(exception.ThrownByFinalHandler, Is.True);
				}
			}
		}
	}
}
