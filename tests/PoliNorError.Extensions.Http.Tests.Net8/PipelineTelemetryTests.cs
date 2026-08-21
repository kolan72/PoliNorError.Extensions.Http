using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class PipelineTelemetryTests
	{
		// --- Helpers ----------------------------------------------------

		private static ActivityListener CreateListener(List<Activity> collector)
			=> CreateListener(PipelineTelemetry.SourceName, collector);

		private static ActivityListener CreateListener(string sourceName, List<Activity> collector)
		{
			var listener = new ActivityListener
			{
				ShouldListenTo = s => s.Name == sourceName,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
				// Safety net for activities created via a parent-id string rather than a context.
				SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
				ActivityStarted = a => collector.Add(a)
			};
			ActivitySource.AddActivityListener(listener);
			return listener;
		}

		[Test]
		public void Should_Nest_Inner_Pipeline_Activity_Under_Outer_Pipeline_Activity()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var pipelineActivities = activities
				.Where(a => a.OperationName == PipelineTelemetry.PipelineOperationName)
				.ToList();

			// The outer handler is outermost in the DelegatingHandler chain, so its activity
			// is started first; the inner (final) handler's activity is started next.
			Assert.That(pipelineActivities.Count, Is.GreaterThanOrEqualTo(2),
				"Expected at least an outer and an inner pipeline activity");

			var outer = pipelineActivities[0];
			var inner = pipelineActivities[1];

			// The outer activity is the root of the captured pipeline span tree: none of the
			// captured pipeline activities is its parent.
			Assert.That(
				pipelineActivities.All(a => a.Id != outer.ParentId),
				"Outer pipeline activity must be the root of the pipeline span tree");

			// The inner activity must be a direct child of the outer one.
			Assert.That(inner.ParentId, Is.EqualTo(outer.Id),
				"Inner pipeline activity must have the outer pipeline activity as its parent");
			Assert.That(inner.Parent, Is.SameAs(outer),
				"Inner pipeline activity's Parent must reference the outer pipeline activity object");
		}

		// --- Success path ----------------------------------------------

		[Test]
		public async Task Should_Emit_Pipeline_Activity_On_Successful_Request()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeSussessHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
			var request = new HttpRequestMessage(HttpMethod.Get, "http://any.localhost/any");

			var response = await client.SendAsync(request);

			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);
			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem("polinorerror.pipeline.result"), Is.EqualTo("success"));
			Assert.That(pipelineActivity.GetTagItem("polinorerror.pipeline.is_final_handler"), Is.EqualTo(true));
		}

		// --- Retry failure path ----------------------------------------

		[Test]
		public void Should_Emit_Failed_Activity_When_All_Retries_Exhausted()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(2))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);
			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem("polinorerror.pipeline.result"), Is.EqualTo("failed"));
			Assert.That(pipelineActivity.GetTagItem("polinorerror.pipeline.policy.type"), Is.EqualTo("RetryPolicy"));
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
		}

		// --- Cancellation path -----------------------------------------

		[Test]
		public void Should_Emit_Canceled_Activity_When_CancellationRequested()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			using var cts = new CancellationTokenSource();
			cts.Cancel();

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.None()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var exception = Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any"), cts.Token));

			Assert.That(exception.IsCanceled, Is.True);

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);
			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem("polinorerror.pipeline.result"), Is.EqualTo("canceled"));
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
		}

		// --- Fallback path ---------------------------------------------

		[Test]
		public async Task Should_Emit_Success_Activity_When_Fallback_Invoked()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddPolicyHandler(new FallbackPolicy()
						.WithAsyncFallbackFunc(_ =>
							Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
					.AddRetryHandler(new RetryPolicy(3))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any"));

			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

			var pipelineActivities = activities
				.Where(a => a.OperationName == PipelineTelemetry.PipelineOperationName)
				.ToList();
			Assert.That(pipelineActivities, Is.Not.Empty);

			var fallbackActivity = pipelineActivities
				.Find(a => string.Equals(a.GetTagItem("polinorerror.pipeline.policy.type") as string, "FallbackPolicy"));
			Assert.That(fallbackActivity, Is.Not.Null);
			Assert.That(fallbackActivity!.GetTagItem("polinorerror.pipeline.result"), Is.EqualTo("success"));
		}

		// --- Policy type tag -------------------------------------------

		[Test]
		public void Should_Tag_Activity_With_Policy_Type()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);

			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem("polinorerror.pipeline.policy.type"), Is.EqualTo("RetryPolicy"));
		}

		// --- No-op when no listener ------------------------------------

		[Test]
		public void Should_Not_Throw_When_No_Listener_Attached()
		{
			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var ex = Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));
			Assert.That(ex, Is.Not.Null);
		}

		// --- Multiple pipeline handlers --------------------------------

		[Test]
		public void Should_Emit_Activities_For_Each_Handler_In_Chain()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var pipelineActivities = activities
				.Where(a => a.OperationName == PipelineTelemetry.PipelineOperationName)
				.ToList();

			// At least 2 handlers in the chain > at least 2 Activities (third may come from framework)
			Assert.That(pipelineActivities.Count, Is.GreaterThanOrEqualTo(2));

			// Each activity must be tagged with our library's policy type
			Assert.That(pipelineActivities.TrueForAll(a => a.GetTagItem("polinorerror.pipeline.policy.type") != null), Is.True);
		}

		// --- Failed response data preserved ----------------------------

		[Test]
		public void Should_Emit_Activity_With_FailedResponseData_On_Filtered_Response()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var fakeHandler = new DelegatingHandlerThatReturnsBadStatusCode(HttpStatusCode.GatewayTimeout);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()))
				.AddHttpMessageHandler(() => fakeHandler);

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var exception = Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);

			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(exception.HasFailedResponse, Is.True);
			Assert.That(exception.FailedResponseData.StatusCode, Is.EqualTo(HttpStatusCode.GatewayTimeout));
		}

		[Test]
		public void Should_Nest_HttpClient_Activity_Under_Pipeline_Activity()
		{
			var pipelineActivities = new List<Activity>();
			var httpActivities = new List<Activity>();
			using var pipelineListener = CreateListener(PipelineTelemetry.SourceName, pipelineActivities);
			using var httpListener = CreateListener("System.Net.Http", httpActivities);

			// Canned HTTP/1.1 500 response. Connection: close prevents connection pooling so each
			// retry attempt opens a fresh "connection" (new ConnectCallback invocation).
			var canned500 = Encoding.ASCII.GetBytes(
				"HTTP/1.1 500 Internal Server Error\r\n" +
				"Content-Length: 0\r\n" +
				"Connection: close\r\n\r\n");

			var services = new ServiceCollection();
			services
				.AddHttpClient("my-httpclient", c => c.BaseAddress = new Uri("http://127.0.0.1"))
				.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
				{
					ConnectCallback = (ctx, ct) => new ValueTask<Stream>(new CannedResponseStream(canned500))
				})
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			// The built-in HTTP client instrumentation must have fired.
			Assert.That(httpActivities, Is.Not.Empty,
				"Expected at least one System.Net.Http activity to be emitted");
			Assert.That(httpActivities[0].Kind, Is.EqualTo(ActivityKind.Client),
				"System.Net.Http activity must be of kind Client");

			// Exactly one pipeline activity (the single final handler wraps all retry attempts).
			Assert.That(pipelineActivities.Count, Is.EqualTo(1),
				"Expected exactly one pipeline activity for the single final handler");
			var pipelineActivity = pipelineActivities[0];

			// Every HttpClient span must be a direct child of the pipeline span. This holds for
			// all retry attempts because they all execute inside the pipeline activity's `using` scope.
			Assert.That(
				httpActivities.TrueForAll(h => h.ParentId == pipelineActivity.Id),
				"Expected every System.Net.Http activity to nest under the pipeline activity");
			Assert.That(
				httpActivities.TrueForAll(h => h.Parent != null && h.Parent.Id == pipelineActivity.Id),
				"Expected every System.Net.Http activity's Parent to reference the pipeline activity");
		}

		/// <summary>
		/// A <see cref="Stream"/> that discards writes (the outgoing request) and returns a fixed
		/// canned HTTP response on read. Lets a real <see cref="SocketsHttpHandler"/> emit its
		/// built-in "System.Net.Http" activity without any network access.
		/// </summary>
		private sealed class CannedResponseStream : Stream
		{
			private readonly byte[] _response;
			private int _position;

			public CannedResponseStream(byte[] response) => _response = response;

			public override bool CanRead => true;
			public override bool CanWrite => true;
			public override bool CanSeek => false;
			public override long Length => _response.Length;
			public override long Position { get => _position; set => throw new NotSupportedException(); }

			public override void Flush() { }
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();

			public override int Read(byte[] buffer, int offset, int count)
			{
				if (_position >= _response.Length) return 0;
				int n = Math.Min(count, _response.Length - _position);
				Buffer.BlockCopy(_response, _position, buffer, offset, n);
				_position += n;
				return n;
			}

			public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
			{
				if (_position >= _response.Length) return new ValueTask<int>(0);
				int n = Math.Min(buffer.Length, _response.Length - _position);
				_response.AsSpan(_position, n).CopyTo(buffer.Span);
				_position += n;
				return new ValueTask<int>(n);
			}

			public override void Write(byte[] buffer, int offset, int count) { }
			public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
				=> new ValueTask();
		}

		// --- Uncaught-exception robustness ---------------------------------------

		[Test]
		public void Should_Mark_Activity_As_Error_And_Record_Exception_When_Policy_Throws()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddPolicyHandler(new ThrowingPolicy(new InvalidOperationException("Policy threw unexpectedly")))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var thrown = Assert.ThrowsAsync<InvalidOperationException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			Assert.That(thrown.Message, Is.EqualTo("Policy threw unexpectedly"));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);

			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
			Assert.That(pipelineActivity.GetTagItem("polinorerror.pipeline.result"), Is.EqualTo("faulted"));

			var exceptionEvent = pipelineActivity.Events.FirstOrDefault(e => e.Name == "exception");
			Assert.That(exceptionEvent.Tags.FirstOrDefault(t => t.Key == "exception.type").Value,
				Is.EqualTo("System.InvalidOperationException"));
			Assert.That(exceptionEvent.Tags.FirstOrDefault(t => t.Key == "exception.message").Value,
				Is.EqualTo("Policy threw unexpectedly"));
		}

		[Test]
		public void Should_Propagate_Policy_Exception_Even_When_No_Listener_Attached()
		{
			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddPolicyHandler(new ThrowingPolicy(new InvalidOperationException("No listener")))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var thrown = Assert.ThrowsAsync<InvalidOperationException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			Assert.That(thrown.Message, Is.EqualTo("No listener"));
		}

	}
}
