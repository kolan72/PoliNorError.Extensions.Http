using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class PoliNorErrorExtensionsTests
	{
		[Test]
		public void Should_WithRetryAfterHeaderWait_Add_Error_Processor()
		{
			var rp = new RetryPolicy(1);
			Stopwatch sw = null;
			TimeSpan elapsed = TimeSpan.Zero;
			rp
				.WithErrorProcessorOf((_) => sw = Stopwatch.StartNew())
				.WithRetryAfterHeaderWait()
				.WithErrorProcessorOf((_) => elapsed = sw.Elapsed);

			var response = new HttpResponseMessage();
			response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(9));
			var failedResponse = FailedHttpResponseCreator.CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			rp.Handle(() => throw exception);
			Assert.That(elapsed.Milliseconds, Is.GreaterThanOrEqualTo(9));

		}
	}
}
