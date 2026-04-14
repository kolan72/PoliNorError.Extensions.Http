using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	[TestFixture]
	public class FallbackAsyncFuncTests
	{
		[Test]
		public void Should_Store_AsyncFunc_When_Async_Constructor_Is_Called()
		{
			// Arrange
			async Task<HttpResponseMessage> expectedFunc(CancellationToken _)
			{
				await Task.Yield();
				return new HttpResponseMessage(HttpStatusCode.OK);
			}

			// Act
			var fallback = new Fallback(expectedFunc);

			// Assert
			Assert.That(fallback.AsyncFunc, Is.EqualTo((Func<CancellationToken, Task<HttpResponseMessage>>)expectedFunc));
		}

		[Test]
		public void Should_Throw_ArgumentNullException_When_Async_Func_Is_Null()
		{
			// Act & Assert
			Assert.That(() => new Fallback((Func<CancellationToken, Task<HttpResponseMessage>>)null), Throws.ArgumentNullException);
		}

		[Test]
		public async Task Should_Execute_AsyncFunc_When_Converted_To_Delegate()
		{
			// Arrange
			bool funcWasExecuted = false;
			var expectedResponse = new HttpResponseMessage(HttpStatusCode.Created);

			async Task<HttpResponseMessage> TestFunc(CancellationToken _)
			{
				await Task.Yield();
				funcWasExecuted = true;
				return expectedResponse;
			}

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(TestFunc);
			var result = await asyncFunc(CancellationToken.None);

			// Assert
			Assert.That(funcWasExecuted, Is.True);
			Assert.That(result, Is.EqualTo(expectedResponse));
		}

		[Test]
		public async Task Should_Pass_CancellationToken_To_AsyncFunc()
		{
			// Arrange
			CancellationToken capturedToken = default;

			async Task<HttpResponseMessage> TestFunc(CancellationToken ct)
			{
				await Task.Yield();
				capturedToken = ct;
				return new HttpResponseMessage(HttpStatusCode.OK);
			}
			var fallback = new Fallback(TestFunc);

			// Act
			using (var cts = new CancellationTokenSource())
			{
				var expectedToken = cts.Token;
				Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = fallback;
				await asyncFunc(expectedToken);

				// Assert
				Assert.That(capturedToken, Is.EqualTo(expectedToken));
			}
		}

		[Test]
		public void Should_Allow_AsyncFunc_Property_To_Be_Modified()
		{
			// Arrange
			async Task<HttpResponseMessage> originalFunc(CancellationToken _)
			{
				await Task.Delay(1);
				return new HttpResponseMessage(HttpStatusCode.OK);
			}

			async Task<HttpResponseMessage> newFunc(CancellationToken _)
			{
				await Task.Yield();
				return new HttpResponseMessage(HttpStatusCode.BadRequest);
			}

			var fallback = new Fallback(originalFunc)
			{
				// Act
				AsyncFunc = newFunc
			};

			// Assert
			Assert.That(fallback.AsyncFunc, Is.EqualTo((Func<CancellationToken, Task<HttpResponseMessage>>)newFunc));
			Assert.That(fallback.AsyncFunc, Is.Not.EqualTo((Func<CancellationToken, Task<HttpResponseMessage>>)originalFunc));
		}

		[Test]
		public async Task Should_Return_Correct_Response_From_AsyncFunc()
		{
			// Arrange
			const HttpStatusCode expectedStatusCode = HttpStatusCode.Accepted;

			async Task<HttpResponseMessage> TestFunc(CancellationToken _)
			{
				await Task.Yield();
				return new HttpResponseMessage(expectedStatusCode);
			}

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(TestFunc);
			var result = await asyncFunc(CancellationToken.None);

			// Assert
			Assert.That(result.StatusCode, Is.EqualTo(expectedStatusCode));
		}

		[Test]
		public async Task Should_Preserve_Response_Properties_From_AsyncFunc()
		{
			// Arrange
			const HttpStatusCode expectedStatusCode = HttpStatusCode.Created;
			var expectedContent = new StringContent("test content");

			async Task<HttpResponseMessage> TestFunc(CancellationToken _)
			{
				await Task.Yield();
				return new HttpResponseMessage(expectedStatusCode)
				{
					Content = expectedContent
				};
			}

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(TestFunc);
			var result = await asyncFunc(CancellationToken.None);

			// Assert
			Assert.That(result.StatusCode, Is.EqualTo(expectedStatusCode));
			Assert.That(result.Content, Is.EqualTo(expectedContent));
		}

		[Test]
		public async Task Should_Work_With_Cancellation_Token_None()
		{
			// Arrange
			async Task<HttpResponseMessage> TestFunc(CancellationToken _)
			{
				await Task.Delay(1);
				return new HttpResponseMessage(HttpStatusCode.NoContent);
			}

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(TestFunc);
			var result = await asyncFunc(CancellationToken.None);

			// Assert
			Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
		}

		[Test]
		public void Should_Respect_Cancellation_Token_In_AsyncFunc()
		{
			// Arrange
			async Task<HttpResponseMessage> TestFunc(CancellationToken ct)
			{
				await Task.Delay(100, ct);
				return new HttpResponseMessage(HttpStatusCode.OK);
			}

			var fallback = new Fallback(TestFunc);

			// Act & Assert
			using (var cts = new CancellationTokenSource(10))
			{
				Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = fallback;
				Assert.That(async () => await asyncFunc(cts.Token), Throws.InstanceOf<OperationCanceledException>());
			}
		}
	}
}
