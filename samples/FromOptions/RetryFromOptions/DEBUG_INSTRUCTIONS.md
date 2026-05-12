# Debug Instructions for ActivityDumpProcessor

## Issue
The `ActivityDumpProcessor.OnEnd` method is not being called.

## Debug Steps Added

I've added debug output to the `ActivityDumpProcessor` to help diagnose the issue:

1. **Constructor debug**: Prints when the processor is created
2. **OnStart debug**: Prints when an activity starts
3. **OnEnd debug**: Prints when an activity ends

## What to Look For

When you run the sample, you should see:

```
[DEBUG] ActivityDumpProcessor created
```

This confirms the processor was instantiated.

Then during HTTP requests, you should see:

```
[DEBUG] OnStart called for: PoliNorError.Http - HttpPolicyExecution
[DEBUG] OnEnd called for: PoliNorError.Http - HttpPolicyExecution
```

## Possible Issues

### 1. Processor Not Created
If you don't see `[DEBUG] ActivityDumpProcessor created`, the processor wasn't instantiated. This would indicate an issue with the OpenTelemetry configuration.

### 2. OnStart/OnEnd Not Called
If you see the processor created but no OnStart/OnEnd calls, it means:
- Activities are being created but not reaching the processor
- The manual `ActivityListener` might be interfering
- OpenTelemetry's TracerProvider isn't properly initialized

### 3. OnEnd Called But Wrong Source
If you see OnEnd called for other sources (like "System.Net.Http") but not "PoliNorError.Http", it means:
- The ActivitySource name doesn't match
- Activities from our library aren't being created

## How to Run

**IMPORTANT**: Stop any running instances of RetryFromOptions first!

```bash
# Stop the running application (Ctrl+C or close the terminal)

# Then rebuild and run
cd samples/FromOptions/RetryFromOptions
dotnet build
dotnet run
```

## Expected Full Output

```
[DEBUG] ActivityDumpProcessor created

=== Telemetry Features Enabled ===
- Structured Logging: Enabled (via ILoggerFactory)
- Distributed Tracing: Enabled (ActivitySource: PoliNorError.Http)
- OpenTelemetry Export: Console (detailed mode)
- ActivityListener: Added (ensures activities are created)
- Custom Activity Dump: Enabled (shows all tags)

Watch for:
  [Information] HTTP request succeeded after Xms...
  [Warning] HTTP request Failed after Xms...
  === Activity Details === (shows all tags)

[DEBUG] OnStart called for: PoliNorError.Http - HttpPolicyExecution
info: PoliNorError.Extensions.Http.PolicyHttpMessageHandler[0]
      HTTP request succeeded after 245ms. Method=GET, Url=https://catfact.ninja/fact, Policy=RetryPolicy, StatusCode=200, TraceId=..., SpanId=...
[DEBUG] OnEnd called for: PoliNorError.Http - HttpPolicyExecution

=== Activity Details ===
Activity.TraceId:            abc123...
Activity.SpanId:             012ghi...
Activity.Tags:
    http.url: https://catfact.ninja/fact
    http.method: GET
    policy.type: RetryPolicy
    policy.is_final: True
    http.status_code: 200
    http.success: True
========================
```

## If OnEnd Still Not Called

If you see the processor created but OnEnd is never called, try these fixes:

### Fix 1: Remove Manual ActivityListener
The manual `ActivityListener` might be interfering. Comment it out:

```csharp
// var listener = new ActivityListener
// {
//     ShouldListenTo = source => source.Name == "PoliNorError.Http",
//     Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded
// };
// ActivitySource.AddActivityListener(listener);
```

### Fix 2: Force TracerProvider Initialization
Ensure OpenTelemetry is initialized before making requests:

```csharp
await using (var provider = services.BuildServiceProvider())
{
    // Force initialization
    var tracerProvider = provider.GetService<TracerProvider>();
    Console.WriteLine($"[DEBUG] TracerProvider initialized: {tracerProvider != null}");
    
    var service = provider.GetRequiredService<IAskCatService>();
    await CatFactManager.GetCatFactOnRetry(service, loggerTest);
}
```

### Fix 3: Check ActivitySource Instance
Verify the ActivitySource in PolicyHttpMessageHandler is the same one OpenTelemetry is listening to.

## Report Back

After running with debug output, please share:
1. Do you see `[DEBUG] ActivityDumpProcessor created`?
2. Do you see any `[DEBUG] OnStart` or `[DEBUG] OnEnd` messages?
3. What sources are being called (if any)?

This will help identify the exact issue.
