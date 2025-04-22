using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		[TestCase(HttpStatusCode.Continue)]
		[TestCase(HttpStatusCode.Found)]
		[TestCase(HttpStatusCode.Forbidden)]
		[TestCase(HttpStatusCode.GatewayTimeout)]
		public void Should_HandleAllNonSuccessStatusCodes_Filters_Correctly(int statusCodeToCheck)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCodeToCheck)));
			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleNonSuccessfulStatusCodes()))
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
				Assert.That((int)exception.FailedResponseData.StatusCode, Is.EqualTo(statusCodeToCheck));

				Assert.That(exception.InnermostPolicyResult, Is.EqualTo(exception.PolicyResult));
				Assert.That(exception.PolicyResult.Errors.Count(), Is.EqualTo(4));
				Assert.That(exception.PolicyResult.UnprocessedError.GetType(), Is.EqualTo(typeof(FailedHttpResponseException)));

				Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(FailedHttpResponseException)));
			}
		}

		[Test]
		[TestCase((int)HttpStatusCode.OK)]
		[TestCase((int)HttpStatusCode.Created)]
		public async Task Should_HandleAllNonSuccessStatusCodes_Not_Filters_For_Success(int statusCodeToCheck)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCodeToCheck)));
			int i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleNonSuccessfulStatusCodes()))
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
	}
}
