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
		[TestCase(true)]
		[TestCase(false)]
		public void Should_IHttpErrorsToHandle_For_TopHandler_PrecededBy_DelegatingHandler_That_Returns_BadStatusCode_Or_Throws_Exception_Handles_Correctly(bool isHttpStatusCode)
		{
			DelegatingHandler fakeHttpDelegatingHandler = null;
			if (isHttpStatusCode)
			{
				fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.GatewayTimeout)));
			}
			else
			{
				fakeHttpDelegatingHandler = new DelegatingHandlerThatThrowsNotHttpException(DelegatingHandlerThatThrowsNotHttpException.ErrorType.Argument);
			}
			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
														.IncludeException<ArgumentException>())
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.True);
				Assert.That(i, Is.EqualTo(3));
				Assert.That(exception.ThrownByFinalHandler, Is.True);

				if (isHttpStatusCode)
				{
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(FailedHttpResponseException)));
				}
				else
				{
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(ArgumentException)));
				}
			}
		}

		[Test]
		[TestCase(429, true)]
		[TestCase((int)HttpStatusCode.RequestTimeout, true)]
		[TestCase((int)HttpStatusCode.BadGateway, true)]
		[TestCase(429, false)]
		[TestCase((int)HttpStatusCode.RequestTimeout, false)]
		[TestCase((int)HttpStatusCode.BadGateway, false)]
		public void Should_HandleTransientHttpErrors_And_HandleTransientHttpStatusCodes_Filter_TransientStatusCodes(int statusCodeToCheck, bool statusCodesOnly)
		{
			HttpErrorFilter filter = statusCodesOnly ? HttpErrorFilter.HandleTransientHttpStatusCodes() : HttpErrorFilter.HandleTransientHttpErrors();

			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCodeToCheck)));
			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(filter))
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.True);
				Assert.That(i, Is.EqualTo(3));
				Assert.That(exception.ThrownByFinalHandler, Is.True);
			}
		}

		[Test]
		[TestCase((int)HttpStatusCode.Continue, true)]
		[TestCase((int)HttpStatusCode.OK, true)]
		[TestCase((int)HttpStatusCode.Found, true)]
		[TestCase((int)HttpStatusCode.Continue, false)]
		[TestCase((int)HttpStatusCode.OK, false)]
		[TestCase((int)HttpStatusCode.Found, false)]
		public async Task Should_HandleTransientHttpErrors_And_HandleTransientHttpStatusCodes_NotFilter_NotTransientStatusCodes(int statusCodeToCheck, bool statusCodesOnly)
		{
			HttpErrorFilter filter = statusCodesOnly ? HttpErrorFilter.HandleTransientHttpStatusCodes() : HttpErrorFilter.HandleTransientHttpErrors();

			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCodeToCheck)));
			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(filter))
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var res = await sut.SendAsync(request);
				Assert.That((int)res.StatusCode, Is.EqualTo(statusCodeToCheck));
			}
		}

		[Test]
		public void Should_HandleTransientHttpErrors_Filters_HttpRequestException()
		{
			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				Assert.That(exception.IsErrorExpected, Is.True);
				Assert.That(i, Is.EqualTo(3));
				Assert.That(exception.ThrownByFinalHandler, Is.True);
				Assert.That(exception.HasFailedResponse, Is.False);
			}
		}
	}
}
