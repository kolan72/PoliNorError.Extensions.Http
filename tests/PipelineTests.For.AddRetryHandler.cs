using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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

		[Test]
		public void Should_Handle_Retries_Correctly_When_Single_AddRetryHandler_FromFactory_Is_Used()
		{
			var i = 0;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
																		empyConfig
																		.AddRetryHandler((sp) =>
																			{
																				var countProvider = sp.GetRequiredService<IRetryProvider>();
																				return new RetryPolicy(countProvider.RetryCount).WithErrorProcessorOf((_) => i++);
																			})
																		.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var exception = invoker.InvokeHttpClientWithFailureWithRetryProvider<HttpPolicyResultException>(pipelineFactory);
			Assert.That(exception?.InnerException?.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			Assert.That(i, Is.EqualTo(3));
		}

		[Test]
		public void Should_Handle_Retries_Correctly_When_Single_AddRetryHandler_FromFactoryWithContext_Is_Used()
		{
			var i = 0;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			IPipelineBuilder<int> pipelineFactory(IEmptyPipelineBuilder<int> empyConfig) =>
																		empyConfig
																		.AddRetryHandler((context, _) =>
																			new RetryPolicy(context).WithErrorProcessorOf((__) => i++))
																		.AsFinalHandler(criteria);
			var invoker = new HttpClientWithPipelineInvoker();
			var exception = invoker.InvokeHttpClientWithFailureWithRetryContext<HttpPolicyResultException, int>(pipelineFactory, 3);
			Assert.That(exception?.InnerException?.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			Assert.That(i, Is.EqualTo(3));
		}

		[Test]
		public async Task Should_Handle_Fallback_Correctly_When_Single_AddFallbackHandler_Is_Used()
		{
			var epInvoked = false;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			var fallbackPolicy = new FallbackPolicy()
							.WithFallbackFunc((_) => new HttpResponseMessage() { StatusCode = HttpStatusCode.OK })
							.WithErrorProcessorOf((_) => epInvoked = true);

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
																		empyConfig
																		.AddFallbackHandler(fallbackPolicy)
																		.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCode(pipelineFactory);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(epInvoked, Is.True);
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

			return Send<TFailure>(services);
		}

		public TFailure InvokeHttpClientWithFailureWithRetryProvider<TFailure>(Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory) where TFailure : Exception
		{
			var services = new ServiceCollection();
			services.AddScoped<IRetryProvider, RetryProvider>();

			services
				.AddFakeHttpClient()
				.WithResiliencePipeline(pipelineFactory);

			return Send<TFailure>(services);
		}

		public TFailure InvokeHttpClientWithFailureWithRetryContext<TFailure, TContext>(Func<IEmptyPipelineBuilder<TContext>, IPipelineBuilder<TContext>> pipelineFactory, TContext context) where TFailure : Exception
		{
			var services = new ServiceCollection();
			services
				.AddFakeHttpClient()
				.WithResiliencePipeline(pipelineFactory, context);

			return Send<TFailure>(services);
		}

		private TFailure Send<TFailure>(IServiceCollection services) where TFailure : Exception
		{
			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				return Assert.ThrowsAsync<TFailure>(async () => await sut.SendAsync(request));
			}
		}

		public async Task<HttpStatusCode> InvokeHttpClientWithStatusCode(Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory)
		{
			var services = new ServiceCollection();
			services
				.AddFakeHttpClient()
				.WithResiliencePipeline(pipelineFactory);

			return await SendAndGetStatusCode(services);
		}

		private async Task<HttpStatusCode> SendAndGetStatusCode(IServiceCollection services)
		{
			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");
				return (await sut.SendAsync(request)).StatusCode;
			}
		}
	}

	internal interface IRetryProvider
	{
		int RetryCount { get; }
	}

	internal class RetryProvider : IRetryProvider
	{
		public int RetryCount => 3;
	}
}
