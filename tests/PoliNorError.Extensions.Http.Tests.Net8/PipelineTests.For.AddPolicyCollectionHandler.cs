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
		public async Task Should_Handle_PolicyCollection_Correctly_When_Single_AddPolicyCollectionHandler_Is_Used()
		{
			var retryCount = 0;
			var fallbackInvoked = false;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			var policyCollection = PolicyCollection.Create()
				.WithRetry(3)
				.WithErrorProcessorOf((_) => retryCount++)
				.WithFallback(() =>
				{
					fallbackInvoked = true;
					return new HttpResponseMessage() { StatusCode = HttpStatusCode.OK };
				});

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
				empyConfig
					.AddPolicyCollectionHandler(policyCollection)
					.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCode(pipelineFactory);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(retryCount, Is.EqualTo(3));
			Assert.That(fallbackInvoked, Is.True);
		}

		[Test]
		public async Task Should_Handle_PolicyCollection_Correctly_When_Single_AddPolicyCollectionHandler_FromFactory_Is_Used()
		{
			var retryCount = 0;
			var fallbackInvoked = false;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
				empyConfig
					.AddPolicyCollectionHandler((sp) =>
					{
						var policyCollectionProvider = sp.GetRequiredService<IPolicyCollectionProvider>();
						return PolicyCollection.Create()
							.WithRetry(policyCollectionProvider.RetryCount)
							.WithErrorProcessorOf((_) => retryCount++)
							.WithFallback(() =>
							{
								fallbackInvoked = true;
								return new HttpResponseMessage() { StatusCode = HttpStatusCode.OK };
							});
					})
					.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCodeWithPolicyCollectionProvider(pipelineFactory);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(retryCount, Is.EqualTo(3));
			Assert.That(fallbackInvoked, Is.True);
		}

		[Test]
		public async Task Should_Handle_PolicyCollection_Correctly_When_Single_AddPolicyCollectionHandler_FromFactoryWithContext_Is_Used()
		{
			int contextValue = 2;
			int retriesPerformedFromContext = 0;
			var fallbackInvoked = false;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			IPipelineBuilder<int> pipelineFactory(IEmptyPipelineBuilder<int> empyConfig) =>
				empyConfig
					.AddPolicyCollectionHandler((context, sp) =>
					{
						return PolicyCollection.Create()
							.WithRetry(context)
							.WithErrorProcessorOf((_) => retriesPerformedFromContext++)
							.WithFallback(() =>
							{
								fallbackInvoked = true;
								return new HttpResponseMessage() { StatusCode = HttpStatusCode.OK };
							});
					})
					.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCodeWithPolicyCollectionContext(pipelineFactory, contextValue);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(retriesPerformedFromContext, Is.EqualTo(2));
			Assert.That(fallbackInvoked, Is.True);
		}

		[Test]
		public async Task Should_Execute_Retry_Before_Fallback_In_PolicyCollection()
		{
			var retryCount = 0;
			var fallbackCount = 0;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			var policyCollection = PolicyCollection.Create()
				.WithRetry(2)
				.WithErrorProcessorOf((_) => retryCount++)
				.WithFallback(() =>
				{
					fallbackCount++;
					return new HttpResponseMessage() { StatusCode = HttpStatusCode.OK };
				});

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
				empyConfig
					.AddPolicyCollectionHandler(policyCollection)
					.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCode(pipelineFactory);

			// Retry should be executed first (2 retries)
			Assert.That(retryCount, Is.EqualTo(2));
			// Then fallback should be executed once and succeed
			Assert.That(fallbackCount, Is.EqualTo(1));
			// Fallback returns OK, so the request succeeds
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
		}

		[Test]
		public void Should_Use_Only_Retry_When_PolicyCollection_Contains_Only_Retry()
		{
			var retryCount = 0;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			var policyCollection = PolicyCollection.Create()
				.WithRetry(5)
				.WithErrorProcessorOf((_) => retryCount++);

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
				empyConfig
					.AddPolicyCollectionHandler(policyCollection)
					.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var exception = invoker.InvokeHttpClientWithFailure<HttpPolicyResultException>(pipelineFactory);

			Assert.That(exception?.InnerException?.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			Assert.That(retryCount, Is.EqualTo(5));
		}

		[Test]
		public async Task Should_Return_Success_When_PolicyCollection_Fallback_Succeeds()
		{
			var retryCount = 0;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			var policyCollection = PolicyCollection.Create()
				.WithRetry(1)
				.WithErrorProcessorOf((_) => retryCount++)
				.WithFallback(async (_) =>
				{
					await Task.Delay(1);
					return new HttpResponseMessage() { StatusCode = HttpStatusCode.Accepted };
				});

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
				empyConfig
					.AddPolicyCollectionHandler(policyCollection)
					.AsFinalHandler(criteria);

			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCode(pipelineFactory);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.Accepted));
			Assert.That(retryCount, Is.EqualTo(1));
		}
	}

	internal interface IPolicyCollectionProvider
	{
		int RetryCount { get; }
	}

	internal class PolicyCollectionProvider : IPolicyCollectionProvider
	{
		public int RetryCount => 3;
	}

	internal static class HttpClientWithPipelineInvokerExtensionsForPolicyCollection
	{
		public static async Task<HttpStatusCode> InvokeHttpClientWithStatusCodeWithPolicyCollectionProvider(
			this HttpClientWithPipelineInvoker invoker,
			Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory)
		{
			var policyCollectionProvider = new PolicyCollectionProvider();

			var services = new ServiceCollection();
			services.AddScoped<IPolicyCollectionProvider>((_) => policyCollectionProvider);

			services
				.AddFakeHttpClient()
				.WithResiliencePipeline(pipelineFactory);

			return await SendAndGetStatusCode(services);
		}

		public static async Task<HttpStatusCode> InvokeHttpClientWithStatusCodeWithPolicyCollectionContext<TContext>(
			this HttpClientWithPipelineInvoker invoker,
			Func<IEmptyPipelineBuilder<TContext>, IPipelineBuilder<TContext>> pipelineFactory,
			TContext context)
		{
			var services = new ServiceCollection();

			services
				.AddFakeHttpClient()
				.WithResiliencePipeline(pipelineFactory, context);

			return await SendAndGetStatusCode(services);
		}

		private static async Task<HttpStatusCode> SendAndGetStatusCode(IServiceCollection services)
		{
			using var serviceProvider = services.BuildServiceProvider();
			using var scope = serviceProvider.CreateScope();
			var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
			var request = new HttpRequestMessage(HttpMethod.Get, "/any");
			return (await sut.SendAsync(request)).StatusCode;
		}
	}
}
