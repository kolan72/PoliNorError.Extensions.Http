using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net.Http;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		[TestCase(DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation)]
		[TestCase(DelegatingHandlerThatThrowsNotHttpException.ErrorType.Argument)]
		public void Should_NotHttpException_Be_Handled_By_AllPipelineHandlers(DelegatingHandlerThatThrowsNotHttpException.ErrorType errorType)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatThrowsNotHttpException(errorType);

			var i = 0;
			var k = 0;
			var m = 0;

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => m++))
														.IncludeException<ArgumentException>()
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => k++))
														.IncludeException<ArgumentException>()
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.None())
														.IncludeException<ArgumentException>())
			//Add fake DelegatingHandler as the first handler.
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
					Assert.That(k, Is.EqualTo(0));
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
				}
				else
				{
					Assert.That(exception.IsErrorExpected, Is.True);
					Assert.That(m, Is.EqualTo(3));
					Assert.That(k, Is.EqualTo(12));
					Assert.That(i, Is.EqualTo(48));
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(ArgumentException)));
				}
				Assert.That(exception.FailedResponseData, Is.Null);
				Assert.That(exception.ThrownByFinalHandler, Is.False);
			}
		}

		[Test]
		[TestCase(DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation)]
		[TestCase(DelegatingHandlerThatThrowsNotHttpException.ErrorType.Argument)]
		public void Should_NotHttpException_Be_Handled_By_AllPipelineHandlers_With_Handler_Throwing_Exception_Between(DelegatingHandlerThatThrowsNotHttpException.ErrorType errorType)
		{
			var fakeHttpDelegatingHandler = new DelegatingHandlerThatThrowsNotHttpException(DelegatingHandlerThatThrowsNotHttpException.ErrorType.Argument);

			int i = 0;
			int k = 0;
			int m = 0;
			int o = 0;

			PolicyWithNotFilterableError testPolicy;

			if (errorType == DelegatingHandlerThatThrowsNotHttpException.ErrorType.InvalidOperation)
			{
				testPolicy = new PolicyWithNotFilterableError(() => throw new InvalidOperationException("Test"), typeof(InvalidOperationException));
			}
			else
			{
				testPolicy = new PolicyWithNotFilterableError(() => throw new ArgumentException("Test"), typeof(ArgumentException));
			}

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig

														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => o++))
														.IncludeException<ArgumentException>()
														//Possibly replace exception type here
														.AddPolicyHandler(testPolicy)

														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => m++))
														.IncludeException<ArgumentException>()
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => k++))
														.IncludeException<ArgumentException>()
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(HttpErrorFilter.None())
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
					Assert.That(o, Is.EqualTo(0));
					Assert.That(m, Is.EqualTo(3));
					Assert.That(k, Is.EqualTo(12));
					Assert.That(i, Is.EqualTo(48));
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
				}
				else
				{
					Assert.That(o, Is.EqualTo(3));
					Assert.That(m, Is.EqualTo(12));
					Assert.That(k, Is.EqualTo(48));
					Assert.That(i, Is.EqualTo(192));
					Assert.That(exception.InnerException.GetType(), Is.EqualTo(typeof(ArgumentException)));
				}

				Assert.That(exception.ThrownByFinalHandler, Is.False);
				Assert.That(exception.IsErrorExpected, Is.False);
			}
		}
	}
}
