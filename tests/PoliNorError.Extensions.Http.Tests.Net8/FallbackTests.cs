using System.Net;

namespace PoliNorError.Extensions.Http.Tests
{
	[TestFixture]
	public class FallbackTests
	{
		[Test]
		public void Should_Store_Func_When_Constructor_Is_Called()
		{
			// Arrange
			static HttpResponseMessage expectedFunc(CancellationToken _) => new(HttpStatusCode.OK);

			// Act
			var fallback = new Fallback(expectedFunc);

			// Assert
			Assert.That(fallback.Func, Is.EqualTo(expectedFunc));
		}

		[Test]
		public void Should_Throw_ArgumentNullException_When_Func_Is_Null()
		{
			// Act & Assert
			Assert.That(() => new Fallback((Func<CancellationToken, HttpResponseMessage>?)null), Throws.ArgumentNullException);
		}

		[Test]
		public async Task Should_Convert_To_Async_Func_Implicitly()
		{
			// Arrange
			var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(_ => expectedResponse);
			var result = await asyncFunc(CancellationToken.None);

			using (Assert.EnterMultipleScope())
			{
				// Assert
				Assert.That(asyncFunc, Is.Not.Null);
				Assert.That(result, Is.EqualTo(expectedResponse));
			}
		}

		[Test]
		public async Task Should_Pass_CancellationToken_Through_Implicit_Conversion()
		{
			// Arrange
			CancellationToken capturedToken = default;
			var fallback = new Fallback(ct =>
			{
				capturedToken = ct;
				return new HttpResponseMessage(HttpStatusCode.OK);
			});

			using var cts = new CancellationTokenSource();
			var expectedToken = cts.Token;

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = fallback;
			await asyncFunc(expectedToken);

			// Assert
			Assert.That(capturedToken, Is.EqualTo(expectedToken));
		}

		[Test]
		public async Task Should_Return_Completed_Task_When_Converted_To_Async_Func()
		{
			// Arrange
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(_ => new HttpResponseMessage(HttpStatusCode.Accepted));

			// Act
			var task = asyncFunc(CancellationToken.None);

			// Assert
			Assert.That(task.IsCompleted, Is.True);
			var result = await task;
			Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
		}

		[Test]
		public async Task Should_Execute_Original_Func_When_Invoked_Through_Implicit_Conversion()
		{
			// Arrange
			bool funcWasExecuted = false;

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = new Fallback(_ =>
			{
				funcWasExecuted = true;
				return new HttpResponseMessage(HttpStatusCode.NoContent);
			});
			await asyncFunc(CancellationToken.None);

			// Assert
			Assert.That(funcWasExecuted, Is.True);
		}

		[Test]
		public async Task Should_Preserve_Response_Properties_After_Implicit_Conversion()
		{
			// Arrange
			const HttpStatusCode expectedStatusCode = HttpStatusCode.Created;
			var expectedContent = new StringContent("test content");
			var fallback = new Fallback(_ => new HttpResponseMessage(expectedStatusCode)
			{
				Content = expectedContent
			});

			// Act
			Func<CancellationToken, Task<HttpResponseMessage>> asyncFunc = fallback;
			var result = await asyncFunc(CancellationToken.None);

			using (Assert.EnterMultipleScope())
			{
				// Assert
				Assert.That(result.StatusCode, Is.EqualTo(expectedStatusCode));
				Assert.That(result.Content, Is.EqualTo(expectedContent));
			}
		}

		[Test]
		public void Should_Allow_Func_Property_To_Be_Modified()
		{
			// Arrange
			var originalFunc = new Func<CancellationToken, HttpResponseMessage>(_ => new HttpResponseMessage(HttpStatusCode.OK));
			var newFunc = new Func<CancellationToken, HttpResponseMessage>(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
			var fallback = new Fallback(originalFunc)
			{
				// Act
				Func = newFunc
			};

			// Assert
			Assert.That(fallback.Func, Is.EqualTo(newFunc));
			Assert.That(fallback.Func, Is.Not.EqualTo(originalFunc));
		}
	}
}
