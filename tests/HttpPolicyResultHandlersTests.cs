using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class HttpPolicyResultHandlersTests
	{
		[Test]
		public void Should_Attach_SyncHandler_To_RetryPolicy()
		{
			bool invoked = false;
			var handlers = new HttpPolicyResultHandlers();
			var policy = new RetryPolicy(1);

			handlers.AddHandler((_, __) => invoked = true);
			handlers.AttachTo(policy);

			using (var failedHttpResponse = new HttpResponseMessage())
			{
				policy.Handle(() => failedHttpResponse);
				Assert.That(invoked, Is.True);
			}
		}

		[Test]
		public void Should_Attach_AsyncHandler_To_RetryPolicy_For_SyncHandling()
		{
			bool invoked = false;
			var handlers = new HttpPolicyResultHandlers();
			var policy = new RetryPolicy(1);

			handlers.AddHandler((_, __) => { invoked = true; return Task.CompletedTask; });
			handlers.AttachTo(policy);

			using (var failedHttpResponse = new HttpResponseMessage())
			{
				policy.Handle(() => failedHttpResponse);
				Assert.That(invoked, Is.True);
			}
		}

		[Test]
		public async Task Should_Attach_AsyncHandler_To_RetryPolicy_For_AsyncHandling()
		{
			bool invoked = false;
			var handlers = new HttpPolicyResultHandlers();
			var policy = new RetryPolicy(1);

			handlers.AddHandler(async (_, __) => { invoked = true; await Task.Delay(1); });
			handlers.AttachTo(policy);

			using (var failedHttpResponse = new HttpResponseMessage())
			{
				await policy.HandleAsync(async(__) => { await Task.Delay(1); return failedHttpResponse; });
				Assert.That(invoked, Is.True);
			}
		}

		[Test]
		public void Should_ThrowArgumentNullException_When_AddingNullSyncHandler()
		{
			// Arrange
			var handlers = new HttpPolicyResultHandlers();

			// Act & Assert
			Assert.That(() => handlers.AddHandler((Action<PolicyResult<HttpResponseMessage>, CancellationToken>)null),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("syncHandler"));
		}

		[Test]
		public void Should_ThrowArgumentNullException_When_AddingNullAsyncHandler()
		{
			// Arrange
			var handlers = new HttpPolicyResultHandlers();

			// Act & Assert
			Assert.That(() => handlers.AddHandler(null),
				Throws.ArgumentNullException.With.Property("ParamName").EqualTo("asyncHandler"));
		}
	}
}
