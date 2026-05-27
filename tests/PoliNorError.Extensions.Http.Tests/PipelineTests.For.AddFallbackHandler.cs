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

		[Test]
		public async Task Should_Handle_Fallback_Correctly_When_Single_AddFallbackHandler_FromFactory_Is_Used()
		{
			var epInvoked = false;

			var criteria = HttpErrorFilter.HandleHttpRequestException();

			IPipelineBuilder pipelineFactory(IEmptyPipelineBuilder empyConfig) =>
																		empyConfig
																		.AddFallbackHandler(
																			(IServiceProvider sp) =>
																			{
																				var fallbackProvider = sp.GetRequiredService<FallbackFuncsProvider>();
																				return
																					fallbackProvider
																					.ToFallbackPolicy()
																					.WithErrorProcessorOf((_) => epInvoked = true);
																			})
																		.AsFinalHandler(criteria);
			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCodeWithFallbackProvider(pipelineFactory);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(epInvoked, Is.True);
		}

		[Test]
		public async Task Should_Handle_Fallback_Correctly_When_Single_AddFallbackHandler_FromFactoryWithContext_Is_Used()
		{
			int contextValue = 1;
			int contextInProcessor = 0;
			var criteria = HttpErrorFilter.HandleHttpRequestException();

			IPipelineBuilder<int> pipelineFactory(IEmptyPipelineBuilder<int> empyConfig) =>
																		empyConfig
																		.AddFallbackHandler(
																			(context, sp) =>
																			{
																				var fallbackProvider = sp.GetRequiredService<FallbackFuncsProvider>();
																				return
																					fallbackProvider
																					.ToFallbackPolicy()
																					.WithErrorProcessorOf((_) => contextInProcessor = context);
																			})
																		.AsFinalHandler(criteria);
			var invoker = new HttpClientWithPipelineInvoker();
			var statusCode = await invoker.InvokeHttpClientWithStatusCodeWithFallbackProvider(pipelineFactory, contextValue);
			Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(contextInProcessor, Is.EqualTo(1));
		}
	}
}
