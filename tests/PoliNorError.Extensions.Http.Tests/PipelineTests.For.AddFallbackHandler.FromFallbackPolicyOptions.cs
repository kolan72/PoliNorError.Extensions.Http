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
        [TestCase(true)]
        [TestCase(false)]
        public void Should_ConfigureErrorProcessing_WhenSetInFallbackOptions(bool fromAction)
        {
            int invocations = 0;
            void configure(IBulkErrorProcessor bp) => bp.WithErrorProcessorOf((_) => invocations++);

            var services = new ServiceCollection();

            if (fromAction)
            {
                services.AddFakeHttpClient()
                    .WithResiliencePipeline((pipeline) => pipeline
                        .AddFallbackHandler(AsyncFallbackResponse, (opt) => opt.ConfigureErrorProcessing = configure)
                        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
            }
            else
            {
                var options = new FallbackPolicyOptions
                {
                    ConfigureErrorProcessing = configure
                };

                services.AddFakeHttpClient()
                    .WithResiliencePipeline((pipeline) => pipeline
                        .AddFallbackHandler(AsyncFallbackResponse, options)
                        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
            }

            using (var serviceProvider = services.BuildServiceProvider())
            using (var scope = serviceProvider.CreateScope())
            {
                var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
                var request = new HttpRequestMessage(HttpMethod.Get, "/any");

                var response = sut.SendAsync(request).GetAwaiter().GetResult();

                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                Assert.That(invocations, Is.EqualTo(1));
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Should_ConfigurePolicyResultHandling_WhenSetInOptions(bool fromAction)
        {
            var invoked = false;
            Action<IHttpPolicyResultHandlers> configure =
                (handlers) => handlers.AddHandler((PolicyResult<HttpResponseMessage> _) => invoked = true);

            var services = new ServiceCollection();

            if (fromAction)
            {
                services.AddFakeHttpClient()
                    .WithResiliencePipeline((pipeline) => pipeline
                        .AddFallbackHandler(AsyncFallbackResponse, (opt) => opt.ConfigurePolicyResultHandling = configure)
                        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
            }
            else
            {
                var options = new FallbackPolicyOptions
                {
                    ConfigurePolicyResultHandling = configure
                };

                services.AddFakeHttpClient()
                    .WithResiliencePipeline((pipeline) => pipeline
                        .AddFallbackHandler(AsyncFallbackResponse, options)
                        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
            }

            using (var serviceProvider = services.BuildServiceProvider())
            using (var scope = serviceProvider.CreateScope())
            {
                var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
                var request = new HttpRequestMessage(HttpMethod.Get, "/any");

                var response = sut.SendAsync(request).GetAwaiter().GetResult();

                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                Assert.That(invoked, Is.True);
            }
        }

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_ConfigurePolicyName_WhenSetInFallbackOptions(bool fromAction)
		{
			var capturedPolicyName = string.Empty;
			void configure(IHttpPolicyResultHandlers handlers) => handlers.AddHandler((PolicyResult<HttpResponseMessage> pr) => capturedPolicyName = pr.PolicyName);

			var services = new ServiceCollection();

			if (!fromAction)
			{
				var outerPolicyOptions = new FallbackPolicyOptions
				{
					PolicyName = "outerName",
					ConfigurePolicyResultHandling = configure
				};

				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddFallbackHandler(AsyncFallbackResponse, outerPolicyOptions)
															.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddFallbackHandler(
																AsyncFallbackResponse,
																(outopt) => {
																	outopt.PolicyName = "outerName";
																	outopt.ConfigurePolicyResultHandling = configure;
																})
															.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var _ = sut.SendAsync(request).GetAwaiter().GetResult();

				Assert.That(capturedPolicyName, Is.EqualTo("outerName"));
			}
		}

		[Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Should_ConfigureErrorFilter_WhenSetInOptions(bool fromAction)
        {
            var services = new ServiceCollection();

            if (fromAction)
            {
                services.AddFakeHttpClient()
                    .WithResiliencePipeline((pipeline) => pipeline
                        .AddFallbackHandler(AsyncFallbackResponse,
                            (opt) => opt.ConfigureErrorFilter = (ef) => ef.ExcludeError<HttpRequestException>())
                        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
            }
            else
            {
                var options = new FallbackPolicyOptions
                {
                    ConfigureErrorFilter = (ef) => ef.ExcludeError<HttpRequestException>()
                };

                services.AddFakeHttpClient()
                    .WithResiliencePipeline((pipeline) => pipeline
                        .AddFallbackHandler(AsyncFallbackResponse, options)
                        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
            }

            using (var serviceProvider = services.BuildServiceProvider())
            using (var scope = serviceProvider.CreateScope())
            {
                var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
                var request = new HttpRequestMessage(HttpMethod.Get, "/any");

                var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
                Assert.That(exception.IsErrorExpected, Is.False);
            }
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
