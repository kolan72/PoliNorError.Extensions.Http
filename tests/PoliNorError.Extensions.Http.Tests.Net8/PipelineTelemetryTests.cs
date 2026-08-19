using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class PipelineTelemetryTests
	{
		// --- Helpers ----------------------------------------------------

		private static ActivityListener CreateListener(List<Activity> collector)
		{
			var listener = new ActivityListener
			{
				ShouldListenTo = s => s.Name == PipelineTelemetry.SourceName,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
				ActivityStarted = a => collector.Add(a)
			};
			ActivitySource.AddActivityListener(listener);
			return listener;
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
			Assert.That(pipelineActivity!.GetTagItem("pipeline.result"), Is.EqualTo("success"));
			Assert.That(pipelineActivity.GetTagItem("pipeline.is_final_handler"), Is.EqualTo(true));
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
			Assert.That(pipelineActivity!.GetTagItem("pipeline.result"), Is.EqualTo("failed"));
			Assert.That(pipelineActivity.GetTagItem("pipeline.policy.type"), Is.EqualTo("RetryPolicy"));
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
			Assert.That(pipelineActivity!.GetTagItem("pipeline.result"), Is.EqualTo("canceled"));
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
				.Find(a => string.Equals(a.GetTagItem("pipeline.policy.type") as string, "FallbackPolicy"));
			Assert.That(fallbackActivity, Is.Not.Null);
			Assert.That(fallbackActivity!.GetTagItem("pipeline.result"), Is.EqualTo("success"));
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
			Assert.That(pipelineActivity!.GetTagItem("pipeline.policy.type"), Is.EqualTo("RetryPolicy"));
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
			Assert.That(pipelineActivities.TrueForAll(a => a.GetTagItem("pipeline.policy.type") != null), Is.True);
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
	}
}
