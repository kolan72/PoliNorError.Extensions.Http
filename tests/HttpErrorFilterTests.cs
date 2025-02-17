using NUnit.Framework;
using System;
using System.Net;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class HttpErrorFilterTests
	{
		[Test]
		[TestCase(200, false)]
		[TestCase(400, false)]
		[TestCase(500, true)]
		[TestCase(503, true)]
		public void Should_HandleStatusCodeCategory_Work(int statusCodeToCheck, bool resCheck)
		{
			var store = HttpErrorFilter.HandleStatusCodeCategory(StatusCodesCategory.Status5XX);
			Assert.That(store.Contains(statusCodeToCheck), Is.EqualTo(resCheck));
		}

		[Test]
		[TestCase(HttpStatusCode.RequestEntityTooLarge, (HttpStatusCode)429, false)]
		[TestCase(HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestEntityTooLarge, true)]
		public void Should_HandleStatusCode_Work(HttpStatusCode httpStatusCodeFrom, HttpStatusCode httpStatusCodeToCheck, bool expected)
		{
			var store = HttpErrorFilter.HandleStatusCode(httpStatusCodeFrom);
			Assert.That(store.Contains(httpStatusCodeToCheck), Is.EqualTo(expected));
		}

		[Test]
		public void Should_HandleHttpRequestException_Set_HttpRequestExceptionToHandle_In_True()
		{
			var store = HttpErrorFilter.HandleHttpRequestException();
			Assert.That(store.IncludeHttpRequestException, Is.True);
		}

		[Test]
		public void Should_HandleTransientHttpErrors_Set_HttpRequestExceptionToHandle_In_True_And_Contains_Transient_Status_Codes()
		{
			var store = HttpErrorFilter.HandleTransientHttpErrors();

			Assert.That(store.IncludeHttpRequestException, Is.True);
			StoreContainsTransientHttpStatusCodes(store);
		}

		[Test]
		public void Should_OrTransientHttpStatusCodes_Contains_Transient_Status_Codes()
		{
			var store = HttpErrorFilter.HandleStatusCode(HttpStatusCode.Continue)
						.OrTransientHttpStatusCodes();
			StoreContainsTransientHttpStatusCodes(store);
			Assert.That(store.Contains(HttpStatusCode.Continue), Is.True);
		}

		private void StoreContainsTransientHttpStatusCodes(ConfigurableHttpErrorFilter store)
		{
			Assert.That(store.Contains(HttpStatusCode.RequestTimeout), Is.True);
			Assert.That(store.Contains(429), Is.True);

			for (var i = 0; i < 100; i++)
			{
				Assert.That(store.Contains(500 + i), Is.True);
			}
		}

		[Test]
		public void Should_HandleStatusCode_Throw_Exception_If_StatusCode_Is_Not_Valid()
		{
			Assert.Throws<ArgumentException>(() => HttpErrorFilter.HandleStatusCode(1000));
		}

		[Test]
		[TestCase(206, false)]
		[TestCase(429, true)]
		[TestCase(500, true)]
		public void Should_HandleAllNonSuccessfulCodes_Work(int statusCode, bool expected)
		{
			var store = HttpErrorFilter.HandleNonSuccessfulStatusCodes();
			Assert.That(store.Contains(statusCode), Is.EqualTo(expected));
		}

		[Test]
		public void Should_NoHttpErrorsToHandle_Work()
		{
			var errorsToHandle = HttpErrorFilter.None();
			Assert.That(errorsToHandle.IncludeHttpRequestException, Is.False);
			Assert.That(errorsToHandle.Contains((int)HttpStatusCode.BadGateway), Is.False);
		}
	}
}
