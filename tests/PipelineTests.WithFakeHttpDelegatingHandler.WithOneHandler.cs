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
		[TestCase(DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation)]
		[TestCase(DelegatingHandlerThatThrowsNotHttpException.ErrorType.Argument)]
		public void Should_ThatIncludeError_For_TopHandler_For_DelegatingHandlerThatThrowsNotHttpException_Handles_Correctly(DelegatingHandlerThatThrowsNotHttpException.ErrorType errorType)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatThrowsNotHttpException(errorType);

			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleNone())
														.IncludeException<ArgumentException>())
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				if (errorType == DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation)
				{
					Assert.That(exception.IsErrorExpected, Is.False);
					Assert.That(i, Is.EqualTo(0));
				}
				else
				{
					Assert.That(exception.IsErrorExpected, Is.True);
					Assert.That(i, Is.EqualTo(3));
				}
				Assert.That(exception.FailedResponseData, Is.Null);
				Assert.That(exception.ThrownByFinalHandler, Is.True);
			}
		}

		[Test]
		[TestCase(HttpStatusCode.OK)]
		[TestCase(HttpStatusCode.Continue)]
		[TestCase(HttpStatusCode.Found)]
		[TestCase(HttpStatusCode.Forbidden)]
		[TestCase(HttpStatusCode.GatewayTimeout)]
		public async Task Should_NoHttpErrorsToHandle_Filters_Correctly_For_Any_Status_Code(HttpStatusCode statusCode)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(statusCode);

			int i = 0;

			var services = new ServiceCollection();

			services
				.AddFakeHttpClient()
				.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleNone()))
				.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var res = await sut.SendAsync(request);
				Assert.That(res.StatusCode, Is.EqualTo(statusCode));
			}
		}
	}
}
