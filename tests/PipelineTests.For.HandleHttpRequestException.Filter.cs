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
		public void Should_HandleHttpRequestException_Filters_Correctly()
		{
			var i = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleHttpRequestException())
														);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));

				Assert.That(exception != null && exception.HasFailedResponse, Is.False);
				Assert.That(exception != null && exception.IsErrorExpected, Is.True);
				Assert.That(i, Is.EqualTo(3));
				Assert.That(exception != null && exception.ThrownByFinalHandler, Is.True);
				Assert.That(exception?.InnerException?.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			}
		}

		[Test]
		[TestCase(StatusCodeFilerType.NoFilter)]
		[TestCase(StatusCodeFilerType.Category)]
		[TestCase(StatusCodeFilerType.Code)]
		public async Task Should_HandleHttpRequestException_No_Filters_BadStatusCode(StatusCodeFilerType statusCodeFilerType)
		{
			var i = 0;
			const HttpStatusCode badStatusCode = HttpStatusCode.BadRequest;

			var filter = HttpErrorFilter.HandleHttpRequestException();
			switch (statusCodeFilerType)
			{
				case StatusCodeFilerType.Code:
					filter.OrStatusCode(HttpStatusCode.BadRequest);
					break;
				case StatusCodeFilerType.Category:
					filter.OrStatusCodeCategory(StatusCodesCategory.Status4XX);
					break;
			}

			var fakeHttpDelegatingHandler = new DelegatingHandlerThatReturnsBadStatusCode(badStatusCode);

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline(empyConfig => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf(_ => i++))
														.AsFinalHandler(filter))
			.AddHttpMessageHandler(() => fakeHttpDelegatingHandler);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				if (statusCodeFilerType == StatusCodeFilerType.NoFilter)
				{
					var result = await sut.SendAsync(request);
					Assert.That(result.StatusCode, Is.EqualTo(badStatusCode));

					Assert.That(i, Is.EqualTo(0));
				}
				else
				{
					var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
					Assert.That(exception.IsErrorExpected, Is.True);
					Assert.That(i, Is.EqualTo(3));
					Assert.That(exception.FailedResponseData, Is.Not.Null);
					Assert.That(exception.ThrownByFinalHandler, Is.True);
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(FailedHttpResponseException)));
				}
			}
		}

		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_HandleStatusCode_Handles_Correctly_For_HttpRequestException(bool withHttpRequestExceptionFilter)
		{
			var filter = HttpErrorFilter.HandleStatusCode(HttpStatusCode.BadRequest);
			if (withHttpRequestExceptionFilter)
			{
				filter.OrHttpRequestException();
			}

			var services = new ServiceCollection();

			var i = 0;

			services.AddFakeHttpClient()
										.WithResiliencePipeline((empyConfig) => empyConfig
																					.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
																					.AsFinalHandler(filter));
			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var res = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));
				if (withHttpRequestExceptionFilter)
				{
					Assert.That(res.IsErrorExpected, Is.True);
					Assert.That(i, Is.EqualTo(3));
				}
				else
				{
					Assert.That(res.IsErrorExpected, Is.False);
					Assert.That(i, Is.EqualTo(0));
				}
				Assert.That(res.InnerException.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			}
		}

		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_TopHandler_Error_Be_Handled_By_PolicyHandler_From_FallbackPolicy(bool withContext)
		{
			var i = 0;

			var services = new ServiceCollection();

			var builder = services.AddFakeHttpClient();

			if (!withContext)
			{
				builder.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new FallbackPolicy()
																			.WithAsyncFallbackFunc((_) => Task.FromResult(new HttpResponseMessage() { StatusCode = HttpStatusCode.OK })))
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleHttpRequestException())
														);
			}
			else
			{
				builder.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler((context, _) => new FallbackPolicy()
																			.WithAsyncFallbackFunc((__) => Task.FromResult(new HttpResponseMessage() { StatusCode = context }))
																			)
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.HandleHttpRequestException())
														, HttpStatusCode.OK);
			}

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var res = await sut.SendAsync(request);
				Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			}
		}

		internal enum StatusCodeFilerType
		{
			NoFilter,
			Code,
			Category
		}
	}
}
