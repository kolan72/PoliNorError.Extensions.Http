using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
    [TestFixture]
	internal partial class PipelineTests
	{
        private static HttpResponseMessage FallbackResponse(CancellationToken _)
            => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        private static Task<HttpResponseMessage> AsyncFallbackResponse(CancellationToken _)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

		[Test]
		public void Should_ThrowArgumentNullException_When_AddFallbackHandlerCalledWithSyncFuncAndNullOptions()
		{
			var builder = PipelineBuilder.Create();

			Assert.That(() => builder.AddFallbackHandler(FallbackResponse, (FallbackPolicyOptions)null),
				Throws.ArgumentNullException);
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public async Task Should_Fallback_With_EmptyOptions(bool sync)
		{
            var emptyOptions = new FallbackPolicyOptions();

			var services = new ServiceCollection();

			if (sync)
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
													    .AddFallbackHandler(FallbackResponse, emptyOptions)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddFallbackHandler(AsyncFallbackResponse, emptyOptions)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var response = await sut.SendAsync(request);
				Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
			}
		}

		[Test]
        public void Should_ReturnNonNullResult_When_AddFallbackHandlerCalledWithSyncFuncAndOptions()
        {
            var builder = PipelineBuilder.Create();
            var options = new FallbackPolicyOptions();

            var result = builder.AddFallbackHandler(FallbackResponse, options);

            Assert.That(result, Is.Not.Null);
        }

		[Test]
		public void Should_ReturnNonNullResult_When_AddFallbackHandlerCalledWithAsyncFuncAndOptions()
		{
			var builder = PipelineBuilder.Create();
			var options = new FallbackPolicyOptions();

			var result = builder.AddFallbackHandler(AsyncFallbackResponse, options);

			Assert.That(result, Is.Not.Null);
		}

		[Test]
        public void Should_ThrowArgumentNullException_When_AddFallbackHandlerCalledWithNullSyncFuncAndOptions()
        {
            var builder = PipelineBuilder.Create();
            var options = new FallbackPolicyOptions();

            Assert.That(() => builder.AddFallbackHandler((Func<CancellationToken, HttpResponseMessage>)null, options),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Should_ThrowArgumentNullException_When_AddFallbackHandlerCalledWithAsyncFuncAndNullOptions()
        {
            var builder = PipelineBuilder.Create();

            Assert.That(() => builder.AddFallbackHandler(AsyncFallbackResponse, (FallbackPolicyOptions)null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Should_InvokeConfigureErrorProcessing_When_AddFallbackHandlerCalledWithOptionsContainingConfigureErrorProcessing()
        {
            var builder = PipelineBuilder.Create();
            var configureInvoked = false;
            var options = new FallbackPolicyOptions
            {
                ConfigureErrorProcessing = _ => configureInvoked = true
			};

            builder.AddFallbackHandler(AsyncFallbackResponse, options);

            Assert.That(configureInvoked, Is.True);
        }

        [Test]
        public void Should_InvokeConfigurePolicyResultHandling_When_AddFallbackHandlerCalledWithOptionsContainingConfigurePolicyResultHandling()
        {
            var builder = PipelineBuilder.Create();
            var configureInvoked = false;
            var options = new FallbackPolicyOptions
            {
                ConfigurePolicyResultHandling = handlers =>
                {
                    configureInvoked = true;
                    handlers.AddHandler((PolicyResult<HttpResponseMessage> _) => { });
                }
            };

            builder.AddFallbackHandler(AsyncFallbackResponse, options);

            Assert.That(configureInvoked, Is.True);
        }

        [Test]
        public void Should_InvokeConfigureErrorFilter_When_AddFallbackHandlerCalledWithOptionsContainingConfigureErrorFilter()
        {
            var builder = PipelineBuilder.Create();
            var configureInvoked = false;
            var options = new FallbackPolicyOptions
            {
                ConfigureErrorFilter = filter =>
                {
                    configureInvoked = true;
                    return filter.IncludeError<HttpRequestException>();
                }
            };

            builder.AddFallbackHandler(AsyncFallbackResponse, options);

            Assert.That(configureInvoked, Is.True);
        }

        [Test]
        public void Should_AllowMixingOverloads_When_ChainingFallbackHandlers()
        {
            var builder = PipelineBuilder.Create();
            var options = new FallbackPolicyOptions();

            var incomplete = builder.AddFallbackHandler(FallbackResponse, options);
            var result = incomplete.AddFallbackHandler(AsyncFallbackResponse, options);

            Assert.That(result, Is.Not.Null);
        }
    }
}
