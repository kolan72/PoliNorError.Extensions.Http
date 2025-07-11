﻿using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class HttpPolicyResultHandlerTests
	{
		[Test]
		public async Task Should_Attach_SyncHandler_And_Invoke_When_Attached()
		{
			// Arrange
			bool handlerCalled = false;
			var syncHandler = new Action<PolicyResult<HttpResponseMessage>, CancellationToken>(
				(_, __) =>
				handlerCalled = true);
			var policyHandler = new HttpPolicyResultHandler(syncHandler);
			var retryPolicy = new RetryPolicy(1);

			// Act
			policyHandler.AttachTo(retryPolicy);
			await retryPolicy.HandleAsync<HttpResponseMessage>(async (_) => {await Task.Delay(1); throw new InvalidOperationException();});

			// Assert
			Assert.That(handlerCalled, Is.True);
		}

		[Test]
		public async Task Should_Attach_AsyncHandler_And_Invoke_When_Attached()
		{
			// Arrange
			bool handlerCalled = false;
			var asyncHandler = new Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task>((_, __) =>
			{
				handlerCalled = true;
				return Task.CompletedTask;
			});

			var policyHandler = new HttpPolicyResultHandler(asyncHandler);
			var retryPolicy = new RetryPolicy(1);

			// Act
			policyHandler.AttachTo(retryPolicy);
			await retryPolicy.HandleAsync<HttpResponseMessage>(async (_) => { await Task.Delay(1); throw new InvalidOperationException();});

			// Assert
			Assert.That(handlerCalled, Is.True);
		}
	}
}
