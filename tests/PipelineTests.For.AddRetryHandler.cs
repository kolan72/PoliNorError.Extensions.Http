using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net.Http;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		public void Should_Handle_Retries_Correctly_When_Single_AddRetryHandler_Is_Used()
		{
			var i = 0;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			var retryPolicy = new RetryPolicy(3).WithErrorProcessorOf((_) => i++);

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
																		empyConfig
																		.AddRetryHandler(retryPolicy)
																		.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var exception = invoker.InvokeHttpClientWithFailure<HttpPolicyResultException>(pipelineFactory);
			Assert.That(exception?.InnerException?.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			Assert.That(i, Is.EqualTo(3));
		}
	}

	internal class HttpClientWithPipelineInvoker
	{
		public TFailure InvokeHttpClientWithFailure<TFailure>(Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory) where TFailure : Exception
		{
			var services = new ServiceCollection();
			services
				.AddFakeHttpClient()
				.WithResiliencePipeline(pipelineFactory);

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				return Assert.ThrowsAsync<TFailure>(async () => await sut.SendAsync(request));
			}
		}
	}
}
