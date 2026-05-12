# RetryFromOptions Sample - With Telemetry

This sample demonstrates how to use the PoliNorError.Extensions.Http library with **structured logging and distributed tracing** enabled.

## Features Demonstrated

### 1. Structured Logging ✅
The sample enables structured logging by passing `ILoggerFactory` to the `WithResiliencePipeline` method:

```csharp
.WithResiliencePipeline(
    (emptyBuilder) => { /* pipeline configuration */ },
    loggerFactory: null // Resolved from DI container
)
```

**What you'll see:**
- `[Information]` logs for successful HTTP requests with **Method, URL, timing, TraceId, and SpanId**
- `[Warning]` logs for failed or canceled requests with **Method, URL, TraceId and SpanId**
- `[Error]` logs for unexpected exceptions with **TraceId and SpanId**
- All logs include structured data: HTTP method, URL, elapsed time, policy type, status codes, trace correlation IDs

### 2. Distributed Tracing (OpenTelemetry) ✅
The sample configures OpenTelemetry to capture distributed traces:

```csharp
// Add ActivityListener to ensure ActivitySource has listeners
var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "PoliNorError.Http",
    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded
};
ActivitySource.AddActivityListener(listener);

services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("PoliNorError.Http")  // <-- HTTP resilience telemetry
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());
```

**What you'll see:**
- Activity traces for each policy execution with TraceId and SpanId
- Tags showing: URL, HTTP method, policy type, status codes
- Activity status (Ok/Error) based on success/failure
- Timing information for each request

### 3. Retry Policy Configuration
The sample uses `RetryPolicyOptions` to configure retry behavior with custom error processing and result handling.

## Running the Sample

```bash
cd samples/FromOptions/RetryFromOptions
dotnet run
```

## Expected Output

You should see output similar to:

```
=== Telemetry Features Enabled ===
- Structured Logging: Enabled (via ILoggerFactory)
- Distributed Tracing: Enabled (ActivitySource: PoliNorError.Http)
- OpenTelemetry Export: Console (detailed mode)
- ActivityListener: Added (ensures activities are created)

Watch for:
  [Information] HTTP request succeeded after Xms...
  [Warning] HTTP request Failed after Xms...
  TraceId and SpanId in log messages

[Information] HTTP request succeeded after 245ms. Method=GET, Url=https://catfact.ninja/fact, Policy=RetryPolicy, StatusCode=200, TraceId=abc123def456789..., SpanId=012ghi345jkl...

Activity.TraceId:            abc123def456789...
Activity.SpanId:             012ghi345jkl...
Activity.TraceState:         (null)
Activity.ParentSpanId:       000000000000...
Activity.ActivitySourceName: PoliNorError.Http
Activity.DisplayName:        HttpPolicyExecution
Activity.Kind:               Client
Activity.StartTime:          2026-05-12T10:30:00.0000000Z
Activity.Duration:           00:00:00.2450000
Activity.Tags:
    http.url: https://catfact.ninja/fact
    http.method: GET
    http.status_code: 200
    policy.type: RetryPolicy
    policy.is_final: True
    http.success: True
Activity.StatusCode:         Ok
Resource associated with Activity:
    service.name: RetryFromOptions-Sample
    service.instance.id: <guid>
```

## Key Observations

1. **Zero Overhead When Disabled**: If you remove the `loggerFactory` parameter, no logging occurs and there's no performance impact.

2. **Rich Telemetry Data**: Each log and trace includes:
   - **HTTP method and URL** for request identification
   - Elapsed time in milliseconds
   - Policy type being executed
   - HTTP status codes
   - Success/failure indicators
   - **TraceId and SpanId for distributed tracing correlation**

3. **OpenTelemetry Integration**: The traces can be exported to any OpenTelemetry-compatible backend (Jaeger, Zipkin, Application Insights, etc.) by changing the exporter.

## Customization

### Change Log Level
Edit `appSettings.json` or modify the logging configuration:

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning); // Only warnings and errors
});
```

### Export to Different Backend
Replace `AddConsoleExporter()` with your preferred exporter:

```csharp
.AddOtlpExporter()           // OpenTelemetry Protocol (OTLP)
.AddJaegerExporter()         // Jaeger
.AddZipkinExporter()         // Zipkin
```

### Disable Telemetry
Simply remove the `loggerFactory` parameter:

```csharp
.WithResiliencePipeline((emptyBuilder) => { /* config */ })
// No loggerFactory = no telemetry overhead
```

## Learn More

- [Telemetry Documentation](../../../src/docs/Telemetry.md)
- [Telemetry Quick Start](../../../src/docs/TELEMETRY_QUICK_START.md)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)


## Troubleshooting

### I don't see any logs
- ✅ Check that you passed `ILoggerFactory` to `WithResiliencePipeline()`
- ✅ Check your log level configuration (must be `Information` or lower)
- ✅ Check that your logging provider is configured (Console, Debug, etc.)

### I don't see any traces (TraceId/SpanId)
- ✅ Check that you added `.AddSource("PoliNorError.Http")` to OpenTelemetry
- ✅ Check that your exporter is configured correctly (`.AddConsoleExporter()`)
- ✅ Verify the console exporter options are set to output to Console
- ✅ Make sure the application waits for traces to flush (add `await Task.Delay(1000)` before exit)
- ✅ Check that `ActivitySource` has listeners by verifying OpenTelemetry is properly initialized

### Traces appear but without TraceId/SpanId details
- ✅ Ensure console exporter is configured with detailed output:
  ```csharp
  .AddConsoleExporter(options =>
  {
      options.Targets = ConsoleExporterOutputTargets.Console;
  })
  ```
- ✅ Verify the OpenTelemetry SDK is initialized before making HTTP requests

### Performance is slow
- ✅ Disable logging in production if not needed
- ✅ Use sampling for tracing (don't trace 100% of requests)
- ✅ Check your log level (use `Warning` or `Error` in production)
