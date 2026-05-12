using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoliNorError.Extensions.Http;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Examples
{
	/// <summary>
	/// Example demonstrating how to configure structured logging and distributed tracing
	/// for HTTP resilience pipelines.
	/// </summary>
	public class TelemetryExample
	{
		public static void ConfigureServices(IServiceCollection services)
		{
			// 1. Configure logging
			services.AddLogging(builder =>
			{
				builder.AddConsole();
				builder.AddDebug();
				builder.SetMinimumLevel(LogLevel.Information);
			});

			// 2. Configure OpenTelemetry for distributed tracing
			services.AddOpenTelemetry()
				.ConfigureResource(resource => resource
					.AddService("MyApplication"))
				.WithTracing(tracing => tracing
					// Add the PoliNorError.Http activity source
					.AddSource("PoliNorError.Http")
					// Add standard instrumentation
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation()
					// Export to your preferred backend
					.AddOtlpExporter() // Or .AddJaegerExporter(), .AddZipkinExporter(), etc.
				);

			// 3. Configure HttpClient with resilience pipeline and telemetry
			services.AddHttpClient("WeatherApi", client =>
			{
				client.BaseAddress = new Uri("https://api.weather.com");
			})
			.WithResiliencePipeline(
				builder => builder
					.AddRetryHandler(new RetryPolicy(3))
					.AddFallbackHandler(FallbackPolicy.CreateAsync<HttpResponseMessage>(
						async ct => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
						{
							Content = new StringContent("{\"temp\": \"unavailable\"}")
						}))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()),
				// Pass ILoggerFactory to enable logging
				loggerFactory: null // Will be resolved from DI
			);
		}

		public static async Task UseHttpClientWithTelemetry(IServiceProvider serviceProvider)
		{
			var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
			var logger = serviceProvider.GetRequiredService<ILogger<TelemetryExample>>();

			var client = httpClientFactory.CreateClient("WeatherApi");

			try
			{
				logger.LogInformation("Sending request to weather API...");
				
				var response = await client.GetAsync("/forecast");
				
				if (response.IsSuccessStatusCode)
				{
					var content = await response.Content.ReadAsStringAsync();
					logger.LogInformation("Weather data received: {Content}", content);
				}
			}
			catch (HttpPolicyResultException ex)
			{
				// This exception contains rich telemetry data
				logger.LogError(ex, 
					"Request failed. IsCanceled={IsCanceled}, HasFailedResponse={HasFailedResponse}, StatusCode={StatusCode}",
					ex.IsCanceled,
					ex.HasFailedResponse,
					ex.FailedResponseData?.StatusCode);
			}
		}

		/// <summary>
		/// Example: Configure telemetry with custom sampling
		/// </summary>
		public static void ConfigureWithSampling(IServiceCollection services)
		{
			services.AddOpenTelemetry()
				.WithTracing(tracing => tracing
					.AddSource("PoliNorError.Http")
					// Sample 10% of traces in production
					.SetSampler(new TraceIdRatioBasedSampler(0.1))
					.AddOtlpExporter());
		}

		/// <summary>
		/// Example: Disable telemetry for specific clients
		/// </summary>
		public static void ConfigureWithoutTelemetry(IServiceCollection services)
		{
			services.AddHttpClient("InternalApi")
				.WithResiliencePipeline(builder => builder
					.AddRetryHandler(new RetryPolicy(2))
					.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
					// No loggerFactory parameter = no logging
				);
		}

		/// <summary>
		/// Example: Access telemetry data from exception
		/// </summary>
		public static async Task HandleExceptionWithTelemetry(HttpClient client)
		{
			try
			{
				await client.GetAsync("/api/data");
			}
			catch (HttpPolicyResultException ex)
			{
				// Access policy result details
				var policyResult = ex.PolicyResult;
				Console.WriteLine($"Policy failed: {policyResult.IsFailed}");
				Console.WriteLine($"Canceled: {policyResult.IsCanceled}");
				
				// Access innermost policy result (from final handler)
				var innermostResult = ex.InnermostPolicyResult;
				Console.WriteLine($"Error expected: {ex.IsErrorExpected}");
				
				// Access failed response data (if available)
				if (ex.HasFailedResponse)
				{
					var failedResponse = ex.FailedResponseData;
					Console.WriteLine($"Status: {failedResponse.StatusCode}");
					Console.WriteLine($"Content: {failedResponse.Content}");
					Console.WriteLine($"Server: {failedResponse.Server}");
				}
			}
		}
	}
}
