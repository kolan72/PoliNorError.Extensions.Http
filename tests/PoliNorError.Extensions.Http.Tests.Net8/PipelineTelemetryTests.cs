using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
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
			Assert.That(pipelineActivity!.GetTagItem(PipelineTelemetry.ResultTag), Is.EqualTo("success"));
			Assert.That(pipelineActivity.GetTagItem(PipelineTelemetry.IsFinalHandlerTag), Is.EqualTo(true));

			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpRequestMethodTag), Is.EqualTo("GET"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.ServerAddressTag), Is.EqualTo("any.localhost"));
			// Default port (80 for http) is not emitted
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.ServerPortTag), Is.Null);
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.UrlPathTag), Is.EqualTo("/any"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.UrlFullTag), Is.EqualTo("http://any.localhost/any"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag), Is.EqualTo(200));
			// 2xx response on a CLIENT span → Ok (per OTel spec)
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Ok));
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
			Assert.That(pipelineActivity!.GetTagItem(PipelineTelemetry.ResultTag), Is.EqualTo("failed"));
			Assert.That(pipelineActivity.GetTagItem(PipelineTelemetry.PolicyTypeTag), Is.EqualTo("RetryPolicy"));
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Error));

			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpRequestMethodTag), Is.EqualTo("GET"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.UrlPathTag), Is.EqualTo("/any"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag), Is.Null);
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
			Assert.That(pipelineActivity!.GetTagItem(PipelineTelemetry.ResultTag), Is.EqualTo("canceled"));
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Error));

			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpRequestMethodTag), Is.EqualTo("GET"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag), Is.Null);
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
				.Find(a => string.Equals(a.GetTagItem(PipelineTelemetry.PolicyTypeTag) as string, "FallbackPolicy"));
			Assert.That(fallbackActivity, Is.Not.Null);
			Assert.That(fallbackActivity!.GetTagItem(PipelineTelemetry.ResultTag), Is.EqualTo("success"));
			Assert.That(fallbackActivity.Status, Is.EqualTo(ActivityStatusCode.Ok));
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
			Assert.That(pipelineActivity!.GetTagItem(PipelineTelemetry.PolicyTypeTag), Is.EqualTo("RetryPolicy"));
		}

		// --- Policy name tag -------------------------------------------

		[Test]
		public void Should_Tag_Activity_With_Policy_Name_When_Policy_Has_Name()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1).WithPolicyName("myRetryPolicy"))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);

			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem(PipelineTelemetry.PolicyNameTag), Is.EqualTo("myRetryPolicy"));
		}

		[Test]
		public void Should_Not_Emit_Policy_Name_Tag_When_Policy_Has_No_Name()
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
			Assert.That(pipelineActivity!.GetTagItem(PipelineTelemetry.PolicyNameTag), Is.Null);
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
			Assert.That(pipelineActivities.TrueForAll(a => a.GetTagItem(PipelineTelemetry.PolicyTypeTag) != null), Is.True);
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

			// Request tags are present on every pipeline activity.
			Assert.That(pipelineActivity!.GetTagItem(HttpSemanticConventions.HttpRequestMethodTag), Is.EqualTo("GET"));

			// The final handler's activity carries the response status code (504 GatewayTimeout)
			// because result.UnprocessedError is a FailedHttpResponseException on that handler.
			var finalHandlerActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName &&
					a.GetTagItem(PipelineTelemetry.IsFinalHandlerTag) as bool? == true);
			Assert.That(finalHandlerActivity, Is.Not.Null);
			Assert.That(finalHandlerActivity!.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag),
				Is.EqualTo((int)HttpStatusCode.GatewayTimeout));
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
			Assert.That(pipelineActivity.GetTagItem(PipelineTelemetry.ResultTag), Is.EqualTo("faulted"));

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

		// --- HTTP semantic conventions ----------------------------------

		[Test]
		public async Task Should_Tag_Request_With_HTTP_Semantic_Conventions()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var httpMock = new MockHttpMessageHandler();
			httpMock.When("*").Respond(HttpStatusCode.OK, "application/json", "{'name' : 'Test'}");

			var services = new ServiceCollection();
			services.AddHttpClient("my-httpclient")
				.ConfigurePrimaryHttpMessageHandler(() => httpMock)
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com:8443/users/42?sig=secret&foo=bar");

			await client.SendAsync(request);

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);

			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem(HttpSemanticConventions.HttpRequestMethodTag), Is.EqualTo("POST"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.ServerAddressTag), Is.EqualTo("api.example.com"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.ServerPortTag), Is.EqualTo(8443));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.UrlPathTag), Is.EqualTo("/users/42"));
			// sig value must be redacted; foo preserved
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.UrlFullTag),
				Is.EqualTo("https://api.example.com:8443/users/42?sig=REDACTED&foo=bar"));
			Assert.That(pipelineActivity.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag), Is.EqualTo(200));
		}

		[Test]
		public async Task Should_Not_Embed_Credentials_In_Url_Full()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var httpMock = new MockHttpMessageHandler();
			httpMock.When("*").Respond(HttpStatusCode.OK, "application/json", "{'name' : 'Test'}");

			var services = new ServiceCollection();
			services.AddHttpClient("my-httpclient")
				.ConfigurePrimaryHttpMessageHandler(() => httpMock)
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");
			var request = new HttpRequestMessage(HttpMethod.Get, "http://user:pass@myhost.local/app?q=ok");

			await client.SendAsync(request);

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName);

			Assert.That(pipelineActivity, Is.Not.Null);
			// Credentials must not appear in url.full
			var urlFull = pipelineActivity!.GetTagItem(HttpSemanticConventions.UrlFullTag) as string;
			Assert.That(urlFull, Does.Not.Contain("user"));
			Assert.That(urlFull, Does.Not.Contain("pass"));
			Assert.That(urlFull, Is.EqualTo("http://myhost.local/app?q=ok"));
		}

		[Test]
		public async Task Should_Not_Emit_Server_Port_For_Default_Ports()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var httpMock = new MockHttpMessageHandler();
			httpMock.When("*").Respond(HttpStatusCode.OK, "application/json", "{'name' : 'Test'}");

			var services = new ServiceCollection();
			services.AddHttpClient("my-httpclient")
				.ConfigurePrimaryHttpMessageHandler(() => httpMock)
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			// Default HTTPS port (443) and default HTTP port (80) must not be emitted
			await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://secure.example.com/path?a=1"));
			await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://insecure.example.com/path?b=2"));

			var pipelineActivities = activities
				.Where(a => a.OperationName == PipelineTelemetry.PipelineOperationName)
				.ToList();

			Assert.That(pipelineActivities, Is.Not.Empty);
			// Every captured activity must omit server.port for default ports
			Assert.That(pipelineActivities.TrueForAll(a =>
				a.GetTagItem(HttpSemanticConventions.ServerPortTag) == null),
				Is.True, "server.port should not be emitted for default ports (80, 443)");
		}

		[Test]
		public void Should_Tag_Response_Status_Code_On_Failed_Response()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var fakeHandler = new DelegatingHandlerThatReturnsBadStatusCode(HttpStatusCode.BadGateway);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()))
				.AddHttpMessageHandler(() => fakeHandler);

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			Assert.ThrowsAsync<HttpPolicyResultException>(
				() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any")));

			var finalHandlerActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName &&
					a.GetTagItem(PipelineTelemetry.IsFinalHandlerTag) as bool? == true);

			Assert.That(finalHandlerActivity, Is.Not.Null);
			Assert.That(finalHandlerActivity!.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag),
				Is.EqualTo((int)HttpStatusCode.BadGateway));
		}

		[Test]
		public void Should_Not_Tag_Response_Status_Code_On_Non_Http_Error()
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
		// HttpRequestException (network failure) carries no HTTP status code
		Assert.That(pipelineActivity!.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag), Is.Null);
	}

		// --- OTel CLIENT span status from HTTP status code -----------------

		[Test]
		public async Task Should_Mark_Activity_As_Error_When_Success_Has_5xx_Status_Code()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var fakeHandler = new DelegatingHandlerThatReturnsBadStatusCode(HttpStatusCode.ServiceUnavailable);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					// None() means no status codes are filtered, so 503 passes through
					// as a successful policy result — but the span must still be Error
					// per OTel CLIENT span rules (5xx MUST be Error).
					.AsFinalHandler(HttpErrorFilter.None()))
				.AddHttpMessageHandler(() => fakeHandler);

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any"));

			var pipelineActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName &&
					a.GetTagItem(PipelineTelemetry.IsFinalHandlerTag) as bool? == true);

			Assert.That(pipelineActivity, Is.Not.Null);
			Assert.That(pipelineActivity!.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag),
				Is.EqualTo((int)HttpStatusCode.ServiceUnavailable));
			Assert.That(pipelineActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
		}

		[Test]
		public async Task Should_Mark_Activity_As_Error_When_Success_Has_4xx_Status_Code()
		{
			var activities = new List<Activity>();
			using var listener = CreateListener(activities);

			var fakeHandler = new DelegatingHandlerThatReturnsBadStatusCode(HttpStatusCode.NotFound);

			var services = new ServiceCollection();
			services.AddFakeHttpClient()
				.WithResiliencePipeline(b => b
					.AddRetryHandler(new RetryPolicy(1))
					// None() means no status codes are filtered, so 404 passes through
					// as a successful policy result — but the span must still be Error
					// per OTel CLIENT span rules (4xx SHOULD be Error).
					.AsFinalHandler(HttpErrorFilter.None()))
				.AddHttpMessageHandler(() => fakeHandler);

			using var provider = services.BuildServiceProvider();
			var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("my-httpclient");

			var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/any"));

			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

			var finalHandlerActivity = activities
				.Find(a => a.OperationName == PipelineTelemetry.PipelineOperationName &&
					a.GetTagItem(PipelineTelemetry.IsFinalHandlerTag) as bool? == true);

			Assert.That(finalHandlerActivity, Is.Not.Null);
			Assert.That(finalHandlerActivity!.GetTagItem(HttpSemanticConventions.HttpResponseStatusCodeTag),
				Is.EqualTo((int)HttpStatusCode.NotFound));
			Assert.That(finalHandlerActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
		}

		// --- PipelineTelemetry.Source metadata --------------------------

		[Test]
		public void Source_Should_Not_Be_Null()
		{
			Assert.That(PipelineTelemetry.Source, Is.Not.Null);
		}

		[Test]
		public void Source_Should_Have_Expected_Name()
		{
			Assert.That(PipelineTelemetry.Source.Name, Is.EqualTo(PipelineTelemetry.SourceName));
		}

		[Test]
		public void Source_Should_Have_AssemblyVersion()
		{
			var expectedVersion = typeof(PipelineTelemetry).Assembly.GetName().Version?.ToString();
			Assert.That(PipelineTelemetry.Source.Version, Is.EqualTo(expectedVersion));
		}

		[Test]
		public void Started_Activity_Should_Carry_Source_Name()
		{
			Activity? captured = null;
			using var listener = new ActivityListener
			{
				ShouldListenTo = s => s.Name == PipelineTelemetry.SourceName,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
				ActivityStarted = a => captured = a
			};
			ActivitySource.AddActivityListener(listener);

			using var activity = PipelineTelemetry.Source.StartActivity("test");
			Assert.That(activity, Is.Not.Null);
			Assert.That(captured, Is.Not.Null);
			Assert.That(captured!.Source.Name, Is.EqualTo(PipelineTelemetry.SourceName));
		}

	}
}
