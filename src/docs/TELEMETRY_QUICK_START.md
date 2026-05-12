# Telemetry Quick Start Guide

## 5-Minute Setup

### Step 1: Enable Logging (Optional)

```csharp
// In Startup.cs or Program.cs
services.AddHttpClient("MyApi")
    .WithResiliencePipeline(
        builder => builder
            .AddRetryHandler(new RetryPolicy(3))
            .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()),
        loggerFactory: serviceProvider.GetRequiredService<ILoggerFactory>() // <-- Add this
    );
```

**That's it!** You'll now see logs like:
```
[Information] HTTP request succeeded after 245ms. Policy=RetryPolicy, StatusCode=200
[Warning] HTTP request Failed after 1523ms. Policy=RetryPolicy, IsFinalHandler=True
```

### Step 2: Enable Distributed Tracing (Optional)

```csharp
// Add NuGet package: OpenTelemetry.Exporter.OpenTelemetryProtocol

services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("PoliNorError.Http") // <-- Add this line
        .AddOtlpExporter());
```

**That's it!** Traces will now appear in your observability platform (Jaeger, Zipkin, Application Insights, etc.)

## Common Scenarios

### Scenario 1: I want logging in development, but not in production
```csharp
var loggerFactory = builder.Environment.IsDevelopment() 
    ? serviceProvider.GetRequiredService<ILoggerFactory>()
    : null;

services.AddHttpClient("MyApi")
    .WithResiliencePipeline(builder => ..., loggerFactory);
```

### Scenario 2: I want different log levels per environment
```csharp
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "PoliNorError.Extensions.Http": "Information"
    }
  }
}

// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "PoliNorError.Extensions.Http": "Warning"
    }
  }
}
```

### Scenario 3: I want to sample traces (only 10% in production)
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("PoliNorError.Http")
        .SetSampler(new TraceIdRatioBasedSampler(0.1)) // 10% sampling
        .AddOtlpExporter());
```

### Scenario 4: I want to access telemetry data in my code
```csharp
try
{
    await httpClient.GetAsync("/api/data");
}
catch (HttpPolicyResultException ex)
{
    // Access rich telemetry data
    Console.WriteLine($"Failed: {ex.IsCanceled}");
    Console.WriteLine($"Status: {ex.FailedResponseData?.StatusCode}");
    Console.WriteLine($"Expected Error: {ex.IsErrorExpected}");
}
```

## Troubleshooting

### I don't see any logs
- ✅ Check that you passed `ILoggerFactory` to `WithResiliencePipeline()`
- ✅ Check your log level configuration (must be `Information` or lower)
- ✅ Check that your logging provider is configured (Console, Debug, etc.)

### I don't see any traces
- ✅ Check that you added `.AddSource("PoliNorError.Http")` to OpenTelemetry
- ✅ Check that your exporter is configured correctly
- ✅ Check that sampling isn't filtering out all traces

### Performance is slow
- ✅ Disable logging in production if not needed
- ✅ Use sampling for tracing (don't trace 100% of requests)
- ✅ Check your log level (use `Warning` or `Error` in production)

## Performance Tips

| Configuration | Overhead | When to Use |
|--------------|----------|-------------|
| No telemetry | 0μs | Production (if you don't need observability) |
| Logging only (Warning+) | ~2μs | Production (minimal overhead) |
| Logging only (Information+) | ~10μs | Development, staging |
| Tracing (10% sampling) | ~1μs | Production (recommended) |
| Tracing (100% sampling) | ~5μs | Development, debugging |
| Both (full) | ~15μs | Development only |

**Recommendation for Production:** Logging at `Warning` level + Tracing with 10% sampling

## Next Steps

- 📖 Read the [full telemetry documentation](Telemetry.md)
- 💻 Check out [complete examples](TelemetryExample.cs)
- 🔍 Learn about [OpenTelemetry best practices](https://opentelemetry.io/docs/instrumentation/net/)
