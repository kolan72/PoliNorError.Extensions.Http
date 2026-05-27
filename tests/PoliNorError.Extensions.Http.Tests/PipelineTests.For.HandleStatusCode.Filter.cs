using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_HandleStatusCode_Filters_Correctly(bool unsatisfied)
		{
			DelegatingHandlerThatReturnsBadStatusCode fakeHttpDelegatingHandler;
			const HttpStatusCode satisfiedStatusCode = HttpStatusCode.GatewayTimeout;

			if (unsatisfied)
			{
				fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
			}
			else
			{
				fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage(satisfiedStatusCode)));
			}
			var i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleStatusCode(satisfiedStatusCode))
														)
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				if (unsatisfied)
				{
					var res = await sut.SendAsync(request);
					Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
				}
				else
				{
					var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
					Assert.That(exception.IsErrorExpected, Is.True);
					Assert.That(i, Is.EqualTo(3));
					Assert.That(exception.HasFailedResponse, Is.True);
					Assert.That(exception.FailedResponseData, Is.Not.Null);
					Assert.That(exception.FailedResponseData.StatusCode, Is.EqualTo(satisfiedStatusCode));
					Assert.That(exception.ThrownByFinalHandler, Is.True);
				}
			}
		}

		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_HandleStatusCode_Filters_Correctly_WithManyHandlers(bool unsatisfied)
		{
			DelegatingHandlerThatReturnsBadStatusCode fakeHttpDelegatingHandler;

			if (unsatisfied)
			{
				fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
			}
			else
			{
				fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.GatewayTimeout)));
			}
			int i = 0;
			int k = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => k++))
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleStatusCode(HttpStatusCode.GatewayTimeout))
														)
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				if (unsatisfied)
				{
					var res = await sut.SendAsync(request);
					Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
				}
				else
				{
					var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
					Assert.That(exception.IsErrorExpected, Is.True);
					Assert.That(i, Is.EqualTo(12));
					Assert.That(k, Is.EqualTo(3));
					Assert.That(exception.FailedResponseData, Is.Not.Null);
					Assert.That(exception.ThrownByFinalHandler, Is.False);
				}
			}
		}

		[Test]
		[TestCase(false, HttpStatusCode.GatewayTimeout)]
		[TestCase(true, HttpStatusCode.BadRequest)]
		public async Task Should_HandleStatusCodeCategory_Filters_Correctly_WithManyHandlers(bool unsatisfied, HttpStatusCode httpStatusCodeToTest)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult(new HttpResponseMessage(httpStatusCodeToTest)));

			var i = 0;
			var k = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => k++))
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleStatusCodeCategory(StatusCodesCategory.Status5XX)))
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				if (unsatisfied)
				{
					var res = await sut.SendAsync(request);
					Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
				}
				else
				{
					var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
					Assert.That(exception.IsErrorExpected, Is.True);
					Assert.That(i, Is.EqualTo(12));
					Assert.That(k, Is.EqualTo(3));
					Assert.That(exception.FailedResponseData, Is.Not.Null);
					Assert.That(exception.ThrownByFinalHandler, Is.False);
				}
			}
		}
	}
}
