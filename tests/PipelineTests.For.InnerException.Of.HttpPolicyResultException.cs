using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_InnerException_Be_HttpRequestException_For_Out_Of_Pipeline_Source_Of_HttpRequestException_With_Many_PipelineHandlers(bool filterExists)
		{
			var i = 0;
			var k = 0;

			HttpErrorFilterCriteria criteria;
			if (filterExists)
			{
				criteria = HttpErrorFilter.HandleHttpRequestException();
			}
			else
			{
				criteria = HttpErrorFilter.None();
			}

			var services = new ServiceCollection();

			services.AddFakeHttpClient()
			.WithResiliencePipeline((empyConfig) => empyConfig
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => k++))
														.AddPolicyHandler(new RetryPolicy(3).WithErrorProcessorOf((_) => i++))
														.AsFinalHandler(criteria)
														);

			var serviceProvider = services.BuildServiceProvider();

			using (var scope = serviceProvider.CreateScope())
			{
				var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
				var request = new HttpRequestMessage(HttpMethod.Get, "/any");

				var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));

				Assert.That(exception?.HasFailedResponse == true, Is.False);

				if (filterExists)
				{
					Assert.That(exception?.IsErrorExpected == true, Is.True);
					Assert.That(i, Is.EqualTo(12));
				}
				else
				{
					Assert.That(exception?.IsErrorExpected == true, Is.False);
					Assert.That(i, Is.EqualTo(0));
				}

				Assert.That(k, Is.EqualTo(3));

				Assert.That(exception?.ThrownByFinalHandler == true, Is.False);
				Assert.That(exception?.InnerException?.GetType(), Is.EqualTo(typeof(HttpRequestException)));
			}
		}
	}
}
