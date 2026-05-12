using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoliNorError;
using PoliNorError.Extensions.Http;
using Shared;
using System.Diagnostics;

namespace RetryFromOptions
{
	/// <summary>
	/// Custom processor to dump Activity details to console for demonstration purposes.
	/// </summary>
	internal class ActivityDumpProcessor : BaseProcessor<Activity>
	{
		public override void OnEnd(Activity activity)
		{
			if (activity.Source.Name == "PoliNorError.Http")
			{
				Console.WriteLine();
				Console.WriteLine("=== Activity Details ===");
				Console.WriteLine($"Activity.TraceId:            {activity.TraceId}");
				Console.WriteLine($"Activity.SpanId:             {activity.SpanId}");
				Console.WriteLine($"Activity.ParentSpanId:       {activity.ParentSpanId}");
				Console.WriteLine($"Activity.ActivitySourceName: {activity.Source.Name}");
				Console.WriteLine($"Activity.DisplayName:        {activity.DisplayName}");
				Console.WriteLine($"Activity.Kind:               {activity.Kind}");
				Console.WriteLine($"Activity.StartTime:          {activity.StartTimeUtc:O}");
				Console.WriteLine($"Activity.Duration:           {activity.Duration}");
				Console.WriteLine($"Activity.Status:             {activity.Status}");
				
				if (activity.Tags != null && activity.Tags.Any())
				{
					Console.WriteLine("Activity.Tags:");
					foreach (var tag in activity.Tags)
					{
						Console.WriteLine($"    {tag.Key}: {tag.Value}");
					}
				}
				
				Console.WriteLine("========================");
				Console.WriteLine();
			}
		}
	}

	internal static class Program
	{
		private static async Task Main(string[] args)
		{
			// Add a simple ActivityListener to ensure ActivitySource has listeners
			// This ensures activities are created even before OpenTelemetry is fully initialized
			var listener = new ActivityListener
			{
				ShouldListenTo = source => source.Name == "PoliNorError.Http",
				Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded
			};
			ActivitySource.AddActivityListener(listener);

			var services = new ServiceCollection();

			var loggerTest = LogFactory.CreateLogger();
			services.AddSingleton(loggerTest);

			// Configure logging for the application
			services.AddLogging(builder =>
			{
				builder.AddConsole();
				builder.SetMinimumLevel(LogLevel.Information);
			});

			// Configure OpenTelemetry for distributed tracing
			services.AddOpenTelemetry()
				.ConfigureResource(resource => resource
					.AddService("RetryFromOptions-Sample"))
				.WithTracing(tracing => tracing
					// Add the PoliNorError.Http activity source for HTTP resilience telemetry
					.AddSource("PoliNorError.Http")
					// Add standard HTTP client instrumentation
					.AddHttpClientInstrumentation()
					// Add custom processor to dump activity details to console
					.AddProcessor(new ActivityDumpProcessor())
					// Export traces to console with detailed output
					.AddConsoleExporter(options =>
					{
						options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
					})
					// Set to always sample for demo purposes
					.SetSampler(new AlwaysOnSampler()));

			services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

			_ = services
				.AddConfig()
				.AddCatHttpClient()
				// Enable telemetry - ILoggerFactory will be resolved from DI
				.WithResiliencePipeline(
					(emptyBuilder) =>
					{
						return emptyBuilder
								.AddPolicyHandler(CatPolicies
													.GetOuterRetryPolicy(loggerTest))
								.AddRetryHandler(3, GetFinalHandlerRetryPolicyOptions(loggerTest))
								.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
					},
					// Pass ILoggerFactory to enable structured logging and telemetry
					loggerFactory: null // Will be resolved from DI container
				)
				//This handler is used here to mimic service resiliency problems.
				.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			UtilsConsole.PrintHello();
			Console.WriteLine();
			Console.WriteLine("=== Telemetry Features Enabled ===");
			Console.WriteLine("- Structured Logging: Enabled (via ILoggerFactory)");
			Console.WriteLine("- Distributed Tracing: Enabled (ActivitySource: PoliNorError.Http)");
			Console.WriteLine("- OpenTelemetry Export: Console (detailed mode)");
			Console.WriteLine("- ActivityListener: Added (ensures activities are created)");
			Console.WriteLine("- Custom Activity Dump: Enabled (shows all tags)");
			Console.WriteLine();
			Console.WriteLine("Watch for:");
			Console.WriteLine("  [Information] HTTP request succeeded after Xms...");
			Console.WriteLine("  [Warning] HTTP request Failed after Xms...");
			Console.WriteLine("  === Activity Details === (shows all tags)");
			Console.WriteLine();

			Thread.Sleep(1000);

			await using (var provider = services.BuildServiceProvider())
			{
				// Force OpenTelemetry to initialize by resolving TracerProvider
				var tracerProvider = provider.GetService<TracerProvider>();
				
				var service = provider.GetRequiredService<IAskCatService>();
				await CatFactManager.GetCatFactOnRetry(service, loggerTest);
			}

			// Give OpenTelemetry time to flush traces
			await Task.Delay(2000);

			listener.Dispose();

			UtilsConsole.PrintBye();
		}

		private static RetryPolicyOptions GetFinalHandlerRetryPolicyOptions(ILogger logger)
		{
			const string policyName = "FinalHandlerAskCatRetryPolicy";
			return new RetryPolicyOptions()
			{
				PolicyName = policyName,

				ConfigureErrorProcessing = (bp) =>
					bp.WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
					{
						logger.LogError(ex,
										"Policy {PolicyName} handled an exception on attempt {Attempt}:",
										policyName,
										pi.GetRetryCount() + 1);
						if (ex is FailedHttpResponseException failedException)
						{
							logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}", failedException.FailedResponseData.StatusCode);
						}
					})
					.WithDelayBetweenRetries((_, __) => TimeSpan.FromMilliseconds(1000)),

				ConfigurePolicyResultHandling = (handlers) => handlers.AddHandler
				(
					(pr, _) =>
					{
						if (pr.IsPolicySuccess)
							logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
						else if (pr.IsFailed)
						{
							logger.LogWarning("{Errors} exceptions were thrown during handling by {PolicyName}.",
												pr.Errors.Count(),
												pr.PolicyName);
							if (pr.UnprocessedError is not null)
							{
								logger.LogError(pr.UnprocessedError,
												"UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
												policyName);
							}
						}
					}
				)
			};
		}
	}
}
