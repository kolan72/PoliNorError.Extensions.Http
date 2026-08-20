using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace PoliNorError.Extensions.Http.Tests
{
	internal partial class PipelineTests
	{
		[Test]
		public void Should_Dispose_TerminalResponse_When_FinalHandler_Fails_After_Retries()
		{
			var created = new List<TrackedHttpResponseMessage>();

			var fakeHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ =>
			{
				var resp = new TrackedHttpResponseMessage(HttpStatusCode.ServiceUnavailable);
				created.Add(resp);
				return Task.FromResult<HttpResponseMessage>(resp);
			});

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(config => config
					.AddPolicyHandler(new RetryPolicy(3))
					.AsFinalHandler(HttpErrorFilter.HandleNonSuccessfulStatusCodes()))
				.AddHttpMessageHandler(() => fakeHandler);

			using var serviceProvider = services.BuildServiceProvider();
			using var scope = serviceProvider.CreateScope();
			var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
			var request = new HttpRequestMessage(HttpMethod.Get, "/any");

			var exception = Assert.ThrowsAsync<HttpPolicyResultException>(async () => await sut.SendAsync(request));

			Assert.Multiple(() =>
			{
				Assert.That(exception, Is.Not.Null);
				Assert.That(created.Count, Is.EqualTo(4));
				Assert.That(created[0].IsDisposed, Is.True);
				Assert.That(created[1].IsDisposed, Is.True);
				Assert.That(created[2].IsDisposed, Is.True);
				Assert.That(created[3].IsDisposed, Is.True, "Terminal attempt response must be disposed");
			});
		}

		[Test]
		public async Task Should_Not_Dispose_Response_When_FinalHandler_Succeeds()
		{
			var resp = new TrackedHttpResponseMessage(HttpStatusCode.OK);

			var fakeHandler = new DelegatingHandlerThatReturnsBadStatusCode(_ => Task.FromResult<HttpResponseMessage>(resp));
			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(config => config
					.AddPolicyHandler(new RetryPolicy(3))
					.AsFinalHandler(HttpErrorFilter.HandleNonSuccessfulStatusCodes()))
				.AddHttpMessageHandler(() => fakeHandler);

			using var serviceProvider = services.BuildServiceProvider();
			using var scope = serviceProvider.CreateScope();
			var sut = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
			var request = new HttpRequestMessage(HttpMethod.Get, "/any");

			var result = await sut.SendAsync(request);

			Assert.Multiple(() =>
			{
				Assert.That(result, Is.SameAs(resp));
				Assert.That(resp.IsDisposed, Is.False, "Success response must be handed to the caller undisposed");
			});
			result.Dispose();
		}

		private sealed class TrackedHttpResponseMessage : HttpResponseMessage
		{
			public bool IsDisposed { get; private set; }

			public TrackedHttpResponseMessage(HttpStatusCode statusCode) : base(statusCode) { }

			protected override void Dispose(bool disposing)
			{
				IsDisposed = true;
				base.Dispose(disposing);
			}
		}
	}
}
