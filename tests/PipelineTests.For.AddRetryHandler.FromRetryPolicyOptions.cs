using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		public void Should_ThrowArgumentNullException_WhenOptionsIsNull()
		{
			var storage = new FakeStorage();
			int retryCount = 3;

			Assert.That(
				() => storage.AddRetryHandler(retryCount, null),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("options"));
		}

		[Test]
		public void Should_ConfigureErrorProcessing_WhenSetInOptions()
		{
			int i = 0;
			var options = new RetryPolicyOptions
			{
				ConfigureErrorProcessing = (bp) => bp.WithErrorProcessorOf((_) => i++)
			};

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3, options)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.True);
				Assert.That(i, Is.EqualTo(3));
			}
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_ConfigureErrorFilter_WhenSetInOptions(bool exceptionExpected)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatThrowsNotHttpException(DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation);
			RetryPolicyOptions options;

			if (exceptionExpected)
			{
				options = new RetryPolicyOptions
				{
					ConfigureErrorFilter = (ef) => ef.IncludeError<InvalidOperationException>(),
				};
			}
			else
			{
				options = new RetryPolicyOptions
				{
					ConfigureErrorFilter = (ef) => ef.ExcludeError<InvalidOperationException>(),
				};
			}

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
					.WithResiliencePipeline((empyConfig) => empyConfig
																.AddRetryHandler(3, options)
																.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()))
					.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.EqualTo(exceptionExpected));
			}
		}

		[Test]
		public void Should_ConfigurePolicyResultHandling_WhenSetInOptions()
		{
			var invoked = false;
			var options = new RetryPolicyOptions
			{
				ConfigurePolicyResultHandling = (handlers) => handlers.AddHandler((_, __) => invoked = true)
			};

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3, options)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				_ = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(invoked, Is.True);
			}
		}

		[Test]
		public void Should_ConfigurePolicyName_WhenSetInOptions()
		{
			var outerPolicyOptions = new RetryPolicyOptions
			{
				PolicyName = "outerName",
			};

			var innerPolicyOptions = new RetryPolicyOptions
			{
				PolicyName = "innerName",
			};

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(1, innerPolicyOptions)
														.AddRetryHandler(1, outerPolicyOptions)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception  = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.InnermostPolicyResult.PolicyName, Is.EqualTo("outerName"));
				Assert.That(exception.PolicyResult.PolicyName, Is.EqualTo("innerName"));
			}
		}

		[Test]
		public void Should_Configure_RetryDelay_WhenSetInOptions()
		{
			var rd = new FakeRetryDelay();
			var options = new RetryPolicyOptions
			{
				RetryDelay = rd
			};

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3, options)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				_ = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(rd.AttemptsNumber, Is.EqualTo(3));
			}
		}

		private class FakeStorage : IPolicyHandlerStorage<FakeStorage>
		{
			public List<IPolicyBase> AddedPolicies { get; } = new List<IPolicyBase>();

			public FakeStorage AddPolicyHandler<T>(T policy) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
			{
				AddedPolicies.Add(policy);
				return this;
			}

			public FakeStorage AddPolicyHandler<T>(Func<IServiceProvider, T> policyFactory)
				where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
			{
				throw new NotImplementedException();
			}
		}

		private class FakeRetryDelay : RetryDelay
		{
			public int AttemptsNumber { get; private set; }

			public override TimeSpan GetDelay(int attempt)
			{
				AttemptsNumber++;
				return TimeSpan.Zero;
			}
		}

	}
}
