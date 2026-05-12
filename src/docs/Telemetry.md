# Telemetry and Observability

PoliNorError.Extensions.Http provides built-in support for structured logging and distributed tracing to help you monitor and diagnose HTTP resilience behavior in production.

## Features

### 1. Structured Logging

The library integrates with `Microsoft.Extensions.Logging` to provide detailed, structured logs for all policy executions.

**Log Levels:**
- `Information`: Successful HTTP requests with timing and status code
- `Warning`: Failed or canceled requests with policy details
- `Error`: Unexpected exceptions during policy execution

**Example Log Output:**
```
[Information] HTTP request succeeded after 245ms. Policy=RetryPolicy, StatusCode=200
[Warning] HTTP request Failed after 1523ms. Policy=RetryPolicy, IsFinalHandler=True
[Error] Unexpected error in HTTP resilience pipeline after 89ms. Policy=FallbackPolicy
```

### 2. Distributed Tracing (OpenTelemetry)

The library emits `Activity` events using `System.Diagnostics.ActivitySource` for distributed tracing.

**ActivitySource Name:** `PoliNorError.Http`

**Activity Tags:**
- `http.url`: The request URL
- `http.method`: HTTP method (GET, POST, etc.)
- `http.status_code`: Response status code (on success)
- `policy.type`: The policy type being executed (RetryPolicy, FallbackPolicy, etc.)
- `policy.is_final`: Whether this is the final handler in the pipeline
- `http.success`: Boolean indicating success/failure
- `error.type`: Type of error (Canceled, Failed, or exception type)

**Activity Status:**
- `Ok`: Request succeeded
- `Error`: Request failed with exception details

## Usage

### Enable Logging

Pass an `ILoggerFactory` when configuring your pipeline:

```csharp
services.AddHttpClient("MyClient")
    .WithResiliencePipeline(
        builder => builder
            .AddRetryHandler(retryPolicy)
            .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()),
        loggerFactory: serviceProvider.GetRequiredService<ILoggerFactory>()
    );
```

### Enable OpenTelemetry Tracing

Configure OpenTelemetry to listen to the `PoliNorError.Http` activity source:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddSource("PoliNorError.Http")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

### Disable Telemetry

To disable telemetry, simply don't pass a `ILoggerFactory`:

```csharp
services.AddHttpClient("MyClient")
    .WithResiliencePipeline(builder => builder
        .AddRetryHandler(retryPolicy)
        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
    );
```

## Performance Considerations

- **Logging**: Structured logging uses `ILogger` with message templates for efficient string formatting. Logs are only formatted if the log level is enabled.
- **Tracing**: Activities are only created if there's an active listener. If no tracing is configured, the overhead is minimal (a single boolean check).
- **Timing**: Uses `ValueStopwatch` (a struct) to avoid heap allocations when measuring elapsed time.

## Integration with Application Insights

If you're using Azure Application Insights, the telemetry data will automatically flow into:

- **Requests**: HTTP requests with timing and status codes
- **Dependencies**: Outgoing HTTP calls with retry/fallback information
- **Exceptions**: Failed policy executions with full stack traces
- **Custom Events**: Policy execution metrics

## Example: Querying Telemetry

### Application Insights (KQL)

```kusto
// Find all requests that required retries
dependencies
| where customDimensions.["policy.type"] == "RetryPolicy"
| where success == false
| summarize count() by bin(timestamp, 1h), resultCode
```

### Jaeger/Zipkin (Distributed Tracing)

Look for spans with the name `HttpPolicyExecution` and filter by:
- `policy.type` tag to see which policies are executing
- `error.type` tag to identify failure patterns
- Duration to identify slow requests

## Best Practices

1. **Use Sampling in Production**: Configure OpenTelemetry sampling to avoid overwhelming your telemetry backend
2. **Set Appropriate Log Levels**: Use `Information` or `Warning` in production; `Debug` only in development
3. **Monitor Retry Rates**: High retry rates may indicate upstream service issues
4. **Alert on Error Rates**: Set up alerts when `error.type` appears frequently
5. **Correlate with Business Metrics**: Use correlation IDs to link HTTP failures to business impact
