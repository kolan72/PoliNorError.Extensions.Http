using NUnit.Framework;
using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;

namespace PoliNorError.Extensions.Http.Tests
{
	public class RetryAfterHeaderParserTests
	{
		[Test]
		public void Should_ReturnZero_WhenExceptionIsNotFailedHttpResponseException()
		{
			// Arrange
			var exception = new InvalidOperationException("Test exception");

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);

			// Assert
			Assert.That(result, Is.EqualTo(TimeSpan.Zero));
		}

		[Test]
		public void Should_ReturnZero_WhenRetryAfterHeaderIsNull()
		{
			// Arrange
			var response = new HttpResponseMessage();
			var failedResponse = CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);

			// Assert
			Assert.That(result, Is.EqualTo(TimeSpan.Zero));
		}

		[Test]
		public void Should_ReturnDeltaValue_WhenRetryAfterHeaderHasDelta()
		{
			// Arrange
			var response = new HttpResponseMessage();
			response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
			var failedResponse = CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);

			// Assert
			Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(30)));
		}

		[Test]
		public void Should_ReturnTimeUntilDate_WhenRetryAfterHeaderHasFutureDate()
		{
			// Arrange
			var futureDate = DateTimeOffset.UtcNow.AddSeconds(45);
			var response = new HttpResponseMessage();
			response.Headers.RetryAfter = new RetryConditionHeaderValue(futureDate);
			var failedResponse = CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);
			var expected = futureDate - DateTimeOffset.UtcNow;

			// Assert
			Assert.That(result, Is.EqualTo(expected).Within(TimeSpan.FromMilliseconds(100)));
		}

		[Test]
		public void Should_ReturnZero_WhenDateIsInPast()
		{
			// Arrange
			var pastDate = DateTimeOffset.UtcNow.AddSeconds(-10);
			var response = new HttpResponseMessage();
			response.Headers.RetryAfter = new RetryConditionHeaderValue(pastDate);
			var failedResponse = CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);

			// Assert
			Assert.That(result, Is.EqualTo(TimeSpan.Zero));
		}

		[Test]
		public void Should_ReturnZero_WhenCalculatedTimeIsNegative()
		{
			// Arrange
			var slightlyFutureDate = DateTimeOffset.UtcNow.AddMilliseconds(-50);
			var response = new HttpResponseMessage();
			response.Headers.RetryAfter = new RetryConditionHeaderValue(slightlyFutureDate);
			var failedResponse = CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);

			// Assert
			Assert.That(result, Is.EqualTo(TimeSpan.Zero));
		}

		[Test]
		public void Should_ReturnZero_WhenFailedHttpResponseExceptionNull()
		{
			var result = RetryAfterHeaderParser.GetTime(null);
			Assert.That(result, Is.EqualTo(TimeSpan.Zero));
		}

		[Test]
		public void Should_ClampNegativeValuesToZero()
		{
			// Arrange
			var pastDate = DateTimeOffset.UtcNow.AddSeconds(-5);
			var response = new HttpResponseMessage();
			response.Headers.RetryAfter = new RetryConditionHeaderValue(pastDate);
			var failedResponse = CreateFailedHttpResponse(response);
			var exception = new FailedHttpResponseException(failedResponse);

			// Act
			var result = RetryAfterHeaderParser.GetTime(exception);

			// Assert
			Assert.That(result, Is.EqualTo(TimeSpan.Zero));
		}

		private FailedHttpResponse CreateFailedHttpResponse(HttpResponseMessage response)
		{
			return new FailedHttpResponse
			{
				ResponseHeaders = response.Headers,
				StatusCode = response.StatusCode,
				Content = string.Empty,
				ContentType = "text/plain",
				ResponseUri = new Uri("https://example.com"),
				Version = HttpVersion.Version11
			};
		}
	}
}
