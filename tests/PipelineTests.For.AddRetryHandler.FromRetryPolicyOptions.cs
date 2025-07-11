﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
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
				() => storage.AddRetryHandler(retryCount, (RetryPolicyOptions)null),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("options"));
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_Retry_With_EmptyOptions(bool fromAction)
		{
			var emptyOptions = new RetryPolicyOptions();

			var services = new ServiceCollection();

			if (fromAction)
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
													.AddRetryHandler(3)
													.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3, emptyOptions)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.True);
				Assert.That(exception.InnermostPolicyResult.Errors.Count, Is.EqualTo(4));
			}
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_ConfigureErrorProcessing_WhenSetInOptions(bool fromAction)
		{
			int i = 0;

			void configure(IBulkErrorProcessor bp) => bp.WithErrorProcessorOf((_) => i++);

			var services = new ServiceCollection();

			if (fromAction)
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3,
																		(opt) =>
																			opt.ConfigureErrorProcessing = configure)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				var options = new RetryPolicyOptions
				{
					ConfigureErrorProcessing = configure
				};

				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3, options)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}

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
		[TestCase(true, false)]
		[TestCase(false, false)]
		[TestCase(true, true)]
		[TestCase(false, true)]
		public void Should_ConfigureErrorFilter_WhenSetInOptions(bool exceptionExpected, bool fromAction)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatThrowsNotHttpException(DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation);
			RetryPolicyOptions options;

			var services = new ServiceCollection();
			if (fromAction)
			{
				services.AddFakeHttpClient()
					.WithResiliencePipeline((empyConfig) => empyConfig
																.AddRetryHandler(3,
																				(opt) => opt.ConfigureErrorFilter = GetConfigureErrorFilter())
																.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()))
					.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);
			}
			else
			{
				options = new RetryPolicyOptions()
				{
					ConfigureErrorFilter = GetConfigureErrorFilter()
				};

				services.AddFakeHttpClient()
					.WithResiliencePipeline((empyConfig) => empyConfig
																.AddRetryHandler(3, options)
																.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()))
					.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);
			}
			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.EqualTo(exceptionExpected));
			}

			Func<IEmptyCatchBlockFilter, NonEmptyCatchBlockFilter> GetConfigureErrorFilter()
			{
				if (exceptionExpected)
				{
					return (ef) => ef.IncludeError<InvalidOperationException>();
				}
				else
				{
					return (ef) => ef.ExcludeError<InvalidOperationException>();
				}
			}
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_ConfigurePolicyResultHandling_WhenSetInOptions(bool fromAction)
		{
			var invoked = false;

			void configure(IHttpPolicyResultHandlers handlers) => handlers.AddHandler((_, __) => invoked = true);

			var services = new ServiceCollection();

			if (fromAction)
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddRetryHandler(3,
																			(opt) => opt.ConfigurePolicyResultHandling = configure)
															.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				var options = new RetryPolicyOptions
				{
					ConfigurePolicyResultHandling = configure
				};

				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddRetryHandler(3, options)
															.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}

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
		[TestCase(true)]
		[TestCase(false)]
		public void Should_ConfigurePolicyName_WhenSetInOptions(bool fromAction)
		{
			var services = new ServiceCollection();

			if (!fromAction)
			{
				var outerPolicyOptions = new RetryPolicyOptions
				{
					PolicyName = "outerName",
				};

				var innerPolicyOptions = new RetryPolicyOptions
				{
					PolicyName = "innerName",
				};

				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddRetryHandler(1, innerPolicyOptions)
															.AddRetryHandler(1, outerPolicyOptions)
															.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
															.AddRetryHandler(1, (inopt) => inopt.PolicyName ="innerName")
															.AddRetryHandler(1, (outopt) => outopt.PolicyName = "outerName")
															.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}

			using (var serviceProvider = services.BuildServiceProvider())
			using (var scope = serviceProvider.CreateScope())

			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));

				Assert.That(exception.InnermostPolicyResult.PolicyName, Is.EqualTo("outerName"));

				Assert.That(exception.PolicyResult.PolicyName, Is.EqualTo("innerName"));
			}
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_Configure_RetryDelay_WhenSetInOptions(bool fromAction)
		{
			var rd = new FakeRetryDelay();

			var services = new ServiceCollection();

			if (fromAction)
			{
				services.AddFakeHttpClient()
						.WithResiliencePipeline((empyConfig) => empyConfig
																.AddRetryHandler(3,
																				opt => opt.RetryDelay = rd)
																.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}
			else
			{
				var options = new RetryPolicyOptions
				{
					RetryDelay = rd
				};
				services.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddRetryHandler(3, options)
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
			}

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
