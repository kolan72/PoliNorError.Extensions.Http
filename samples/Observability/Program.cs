using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoliNorError.Extensions.Http;

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
services
    .AddHttpClient("demo", client =>
    {
        client.BaseAddress = new Uri("https://httpstat.us/");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    .WithResiliencePipeline(builder => builder
        .AddRetryHandler(new PoliNorError.RetryPolicy(2))
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
catch (OperationCanceledException)
{
    Console.WriteLine("  Result: OperationCanceledException\n");
}
catch (Exception ex)
{
    Console.WriteLine($"  Result: {ex.GetType().Name}\n");
}

Console.WriteLine("=== Done ===");
Console.WriteLine("\nIn production, replace AddConsoleExporter() with:");
Console.WriteLine("  .AddZipkinExporter()     > Jaeger/Zipkin UI at localhost:16686");
Console.WriteLine("  .AddOtlpExporter()      > Any OTLP collector (Grafana, Datadog, etc.)");
