# How to See Detailed Activity Dump

## The Problem

You asked: "Where to see the detailed Activity dump?"

The OpenTelemetry console exporter was configured but not outputting Activity details visibly in the console.

## The Solution

I've added a **custom ActivityDumpProcessor** to the sample that explicitly dumps all Activity details to the console in a clear, readable format.

## What You'll See Now

When you run the sample (`dotnet run`), you'll see output like this:

### 1. Structured Log Messages (from ILogger)
```
info: PoliNorError.Extensions.Http.PolicyHttpMessageHandler[0]
      HTTP request succeeded after 245ms. Method=GET, Url=https://catfact.ninja/fact, Policy=RetryPolicy, StatusCode=200, TraceId=abc123..., SpanId=012ghi...
```

### 2. Activity Details Dump (from ActivityDumpProcessor)
```
=== Activity Details ===
Activity.TraceId:            abc123def456789012345678901234567
Activity.SpanId:             0123456789abcdef
Activity.ParentSpanId:       0000000000000000
Activity.ActivitySourceName: PoliNorError.Http
Activity.DisplayName:        HttpPolicyExecution
Activity.Kind:               Client
Activity.StartTime:          2026-05-13T10:30:00.0000000Z
Activity.Duration:           00:00:00.2450000
Activity.Status:             Ok
Activity.Tags:
    http.url: https://catfact.ninja/fact
    http.method: GET
    policy.type: RetryPolicy
    policy.is_final: True
    http.status_code: 200
    http.success: True
========================
```

## What's in the Activity Dump?

The Activity dump shows **ALL** the telemetry data:

### Core Activity Information
- **TraceId**: Unique identifier for the entire distributed trace
- **SpanId**: Unique identifier for this specific operation
- **ParentSpanId**: SpanId of the parent operation (if any)
- **ActivitySourceName**: Always "PoliNorError.Http" for our library
- **DisplayName**: "HttpPolicyExecution"
- **Kind**: "Client" (indicates this is a client-side operation)
- **StartTime**: When the operation started (UTC)
- **Duration**: How long the operation took
- **Status**: Ok, Error, or Unset

### Activity Tags (All Metadata)
- **http.url**: The full request URL
- **http.method**: HTTP method (GET, POST, etc.)
- **policy.type**: The policy type name (e.g., "RetryPolicy", "SimplePolicy")
- **policy.is_final**: Whether this is the final handler in the pipeline
- **http.status_code**: HTTP response status code (200, 404, 500, etc.)
- **http.success**: True if successful, False if failed
- **error.type**: Type of error (if failed) - e.g., "Canceled", "Failed", exception type

## How It Works

The `ActivityDumpProcessor` is a custom OpenTelemetry processor that:

1. **Listens** to all Activity completions
2. **Filters** for activities from "PoliNorError.Http" source
3. **Dumps** all Activity properties and tags to the console
4. **Formats** the output in a clear, readable way

### Code Added to Program.cs

```csharp
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
            // ... dumps all properties and tags ...
            Console.WriteLine("========================");
            Console.WriteLine();
        }
    }
}

// Then registered in OpenTelemetry configuration:
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("PoliNorError.Http")
        .AddProcessor(new ActivityDumpProcessor())  // <-- Custom processor
        .AddConsoleExporter());
```

## Running the Sample

**IMPORTANT**: Close any running instances of RetryFromOptions first, then:

```bash
cd samples/FromOptions/RetryFromOptions
dotnet run
```

## What This Solves

✅ **Visibility**: You can now see ALL Activity tags in the console  
✅ **Debugging**: Easy to verify that tags are being set correctly  
✅ **Learning**: Clear demonstration of what data is captured  
✅ **No Guessing**: No need to wonder if OpenTelemetry is working  

## Comparison: Logs vs Activity Dump

### Structured Logs (ILogger)
- **Purpose**: Application logging
- **Content**: Key information for debugging
- **Format**: Human-readable log messages
- **Includes**: Method, URL, Policy, StatusCode, TraceId, SpanId

### Activity Dump (OpenTelemetry)
- **Purpose**: Distributed tracing telemetry
- **Content**: Complete telemetry metadata
- **Format**: Structured Activity properties
- **Includes**: ALL tags, timing, trace context, status

Both are valuable and serve different purposes. The logs give you quick insights, while the Activity dump gives you complete telemetry data for deep analysis.

## Production Use

**Note**: The `ActivityDumpProcessor` is for **demonstration purposes only**. In production:

- Remove the `ActivityDumpProcessor`
- Use a real exporter (Jaeger, Zipkin, Application Insights, OTLP)
- Configure sampling to avoid performance impact
- Use structured logging for operational visibility

Example production configuration:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("PoliNorError.Http")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://your-otlp-endpoint");
        })
        .SetSampler(new TraceIdRatioBasedSampler(0.1))); // Sample 10% of traces
```

This sends telemetry to your observability platform where you can analyze it with proper tools.
