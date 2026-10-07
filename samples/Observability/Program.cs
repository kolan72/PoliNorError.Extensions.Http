using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoliNorError.Extensions.Http;

// =========================================================================
// Observability Sample: OpenTelemetry Integration with Policy Result Handlers
// =========================================================================
//
// This sample demonstrates:
// 1. Basic OpenTelemetry setup with PoliNorError.Extensions.Http
// 2. Using ConfigurePolicyResultHandling to observe policy execution
// 3. Enriching OpenTelemetry spans with custom business-level attributes
// 4. Adding span events to track policy lifecycle moments
//
// Key Pattern: ConfigurePolicyResultHandling provides a hook to:
// - Log policy outcomes (success, failure, cancellation)
// - Access retry counts and error details
// - Enrich the current Activity/Span with custom tags and events
// - Bridge policy-level concerns into distributed tracing
//
// =========================================================================

// --- 1. Configure OpenTelemetry ---------------------------------------
// Subscribe to our library's ActivitySource and print every span to stdout.
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(PipelineTelemetry.SourceName)       // Our library's source
    .AddSource("System.Net.Http")                  // Also capture HttpClient's own spans
    .AddConsoleExporter()                          // Print to console
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PoliNorError.Observability.Demo"))
    .Build();

Console.WriteLine("=== PoliNorError OpenTelemetry Demo ===\n");

// --- 2. Set up IHttpClient with the resilience pipeline ---------------
var services = new ServiceCollection();

// Add logging to see policy result handler output
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

services
    .AddHttpClient("demo", client =>
    {
        client.BaseAddress = new Uri("https://tools-httpstatus.pickup-services.com/");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    .WithResiliencePipeline(builder => builder
        .AddRetryHandler(3, opt =>
        {
            opt.PolicyName = "DemoRetryPolicy";
            
            // ═══════════════════════════════════════════════════════════
            // ConfigurePolicyResultHandling - Observability Hook
            // ═══════════════════════════════════════════════════════════
            // This handler runs after policy execution completes.
            // It provides access to:
            // - PolicyResult with success/failure state
            // - Retry count via result.Errors.Count()
            // - Policy name for correlation
            // - Activity.Current to enrich the OpenTelemetry span
            //
            // Use cases:
            // - Add custom span attributes for business context
            // - Record span events for important moments
            // - Emit custom metrics based on policy outcomes
            // - Correlate logs with distributed traces
            // ═══════════════════════════════════════════════════════════
            
            opt.ConfigurePolicyResultHandling = handlers => handlers.AddHandler((result, ct) =>
            {
                var logger = services.BuildServiceProvider().GetService<ILogger<Program>>();
                
                // Log the policy result for demonstration
                if (result.IsSuccess)
                {
                    logger?.LogInformation(
                        "✓ Policy {PolicyName} succeeded after {ErrorCount} retries", 
                        result.PolicyName, 
                        result.Errors.Count());
                }
                else if (result.IsFailed)
                {
                    logger?.LogWarning(
                        "✗ Policy {PolicyName} failed after {ErrorCount} attempts",
                        result.PolicyName,
                        result.Errors.Count());
                }
                
                // Enrich the current OpenTelemetry span with custom attributes
                var activity = Activity.Current;
                if (activity != null)
                {
                    // Add custom business-level tags that appear in your observability backend
                    // These help answer questions like:
                    // - "How many requests succeeded only after retries?"
                    // - "What's the distribution of retry counts?"
                    activity.SetTag("app.policy.retry_count", result.Errors.Count());
                    activity.SetTag("app.policy.outcome", result.IsPolicySuccess ? "success_after_retry" : "failed");
                    
                    // Add an event to mark policy completion on the span timeline
                    // Events show up as markers in trace visualizers (Jaeger, Zipkin, etc.)
                    activity.AddEvent(new ActivityEvent(
                        "policy.completed",
                        tags: new ActivityTagsCollection
                        {
                            { "policy.name", result.PolicyName },
                            { "policy.success", result.IsSuccess },
                            { "policy.error_count", result.Errors.Count() }
                        }));
                }
            });
        })
        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IHttpClientFactory>();

// --- 3. Scenario A: Successful request (no retry) --------------------
Console.WriteLine("--- Scenario A: Successful request (HTTP 200) ---");
try
{
    var clientA = factory.CreateClient("demo");
    var responseA = await clientA.GetAsync("200");
    Console.WriteLine($"  Status: {responseA.StatusCode}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}\n");
}

// --- 4. Scenario B: Failing request (retries, then failure) -----------
Console.WriteLine("--- Scenario B: Failing request (HTTP 500, 3 attempts) ---");
try
{
    var clientB = factory.CreateClient("demo");
    await clientB.GetAsync("500");
}
catch (Exception ex)
{
    Console.WriteLine($"  Result: {ex.GetType().Name} after retries\n");
}

// --- 5. Scenario C: Cancellation --------------------------------------
Console.WriteLine("--- Scenario C: Pre-canceled request ---");
try
{
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();
    var clientC = factory.CreateClient("demo");
    await clientC.GetAsync("200", cts.Token);
}
catch (HttpPolicyResultException ex) when (ex.IsCanceled)
{
    Console.WriteLine("  Result: OperationCanceledException\n");
}
catch (Exception ex)
{
    Console.WriteLine($"  Result: {ex.GetType().Name}\n");
}

Console.WriteLine("=== Done ===");
Console.WriteLine("\n📊 Observability Tips:");
Console.WriteLine("  • ConfigurePolicyResultHandling enriches spans with business context");
Console.WriteLine("  • Activity.Current.SetTag() adds custom attributes visible in traces");
Console.WriteLine("  • Activity.AddEvent() records important moments in the span timeline");
Console.WriteLine("\nIn production, replace AddConsoleExporter() with:");
Console.WriteLine("  .AddZipkinExporter()     > Zipkin collector/UI at localhost:9411");
Console.WriteLine("  .AddOtlpExporter()       > Any OTLP collector (Grafana, Datadog, Jaeger UI at localhost:16686, etc.)");
