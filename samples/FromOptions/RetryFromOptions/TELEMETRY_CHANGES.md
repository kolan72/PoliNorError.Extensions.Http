# Telemetry Changes to RetryFromOptions Sample

## Summary
Updated the RetryFromOptions sample to demonstrate the new structured logging and distributed tracing features added to PoliNorError.Extensions.Http.

## Changes Made

### 1. Project File (`RetryFromOptions.csproj`)
**Added OpenTelemetry packages:**
```xml
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.10.0" />
```

### 2. Program.cs
**Added OpenTelemetry configuration:**
```csharp
services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("RetryFromOptions-Sample"))
    .WithTracing(tracing => tracing
        .AddSource("PoliNorError.Http")  // HTTP resilience telemetry
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());
```

**Enabled telemetry in pipeline:**
```csharp
.WithResiliencePipeline(
    (emptyBuilder) => { /* configuration */ },
    loggerFactory: null  // Resolved from DI - enables logging
)
```

**Added informational output:**
- Banner explaining telemetry features
- Instructions on what to watch for
- Summary of telemetry data after execution

### 3. Documentation
**Created README.md** with:
- Feature overview
- Usage instructions
- Expected output examples
- Customization options
- Links to full documentation

## What the Sample Demonstrates

### Structured Logging
- Information logs for successful requests with timing
- Warning logs for failed/canceled requests
- Error logs for unexpected exceptions
- All logs include structured data (elapsed time, policy type, status codes)

### Distributed Tracing
- Activity traces for each policy execution
- Rich tags: URL, HTTP method, policy type, status codes
- Activity status (Ok/Error)
- Timing information

### Zero Overhead
- Telemetry can be disabled by removing the `loggerFactory` parameter
- No performance impact when disabled

## Running the Sample

```bash
cd samples/FromOptions/RetryFromOptions
dotnet run
```

## Expected Console Output

You'll see:
1. **Banner** explaining enabled features
2. **Structured logs** from the HTTP resilience pipeline:
   ```
   [Information] HTTP request succeeded after 245ms. Policy=RetryPolicy, StatusCode=200
   ```
3. **OpenTelemetry traces** showing activity details:
   ```
   Activity.DisplayName: HttpPolicyExecution
   Activity.Tags:
       http.url: https://catfact.ninja/fact
       policy.type: RetryPolicy
       http.success: True
   ```
4. **Summary** of telemetry features demonstrated

## Integration with Telemetry.md

This sample now serves as a working example of the features documented in:
- `src/docs/Telemetry.md` - Full telemetry documentation
- `src/docs/TELEMETRY_QUICK_START.md` - Quick start guide
- `src/docs/TelemetryExample.cs` - Code examples

## Next Steps

Users can:
1. Run the sample to see telemetry in action
2. Modify log levels in the configuration
3. Change the OpenTelemetry exporter (Console → OTLP, Jaeger, Zipkin)
4. Disable telemetry to see zero-overhead behavior
5. Use this as a template for their own applications
