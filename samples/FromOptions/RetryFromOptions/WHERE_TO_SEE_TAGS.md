# Where to See Activity Tags

## Question
"Why don't I see tags like 'http.url', 'http.method' in console log? Where can I see them?"

## Answer

Activity tags are set on the **Activity object** for distributed tracing, not directly in log messages. There are two places to see this data:

### 1. In Structured Log Messages (Recommended) ✅

**Now included in logs!** The key tags (http.method, http.url) are now explicitly included in the log messages:

```
info: PoliNorError.Extensions.Http.PolicyHttpMessageHandler[0]
      HTTP request succeeded after 245ms. Method=GET, Url=https://catfact.ninja/fact, Policy=RetryPolicy, StatusCode=200, TraceId=abc123..., SpanId=012ghi...
```

**What's included in logs:**
- ✅ `Method` - HTTP method (GET, POST, etc.)
- ✅ `Url` - Full request URL
- ✅ `Policy` - Policy type name
- ✅ `StatusCode` - HTTP status code
- ✅ `TraceId` - Distributed trace ID
- ✅ `SpanId` - Span ID for correlation
- ✅ `ElapsedMs` - Request duration

### 2. In OpenTelemetry Trace Export

The **complete set of tags** is available in the OpenTelemetry trace export. These tags are set on the Activity object and exported by OpenTelemetry:

**All Activity Tags:**
- `http.url` - Request URL
- `http.method` - HTTP method
- `http.status_code` - Response status code
- `policy.type` - Policy type name
- `policy.is_final` - Whether this is the final handler
- `http.success` - Success/failure indicator
- `error.type` - Error type (if failed)

**Where to see them:**

#### Console Exporter (Current Setup)
The OpenTelemetry console exporter outputs activity details including all tags:

```
Activity.TraceId:            abc123def456789...
Activity.SpanId:             012ghi345jkl...
Activity.Tags:
    http.url: https://catfact.ninja/fact
    http.method: GET
    http.status_code: 200
    policy.type: RetryPolicy
    policy.is_final: True
    http.success: True
```

#### Other Exporters
You can export to any OpenTelemetry-compatible backend:

- **Jaeger** - Visual trace timeline with all tags
- **Zipkin** - Distributed tracing UI
- **Application Insights** - Azure monitoring
- **Grafana Tempo** - Open-source tracing
- **OTLP** - OpenTelemetry Protocol for any backend

Example:
```csharp
.AddJaegerExporter(options =>
{
    options.AgentHost = "localhost";
    options.AgentPort = 6831;
})
```

## Why Two Approaches?

### Structured Logs (ILogger)
- **Purpose**: Immediate visibility in application logs
- **Audience**: Developers, operations teams
- **Format**: Human-readable text
- **Use case**: Debugging, monitoring, alerting
- **Includes**: Key information needed for most scenarios

### Activity Tags (OpenTelemetry)
- **Purpose**: Distributed tracing across services
- **Audience**: Observability platforms, APM tools
- **Format**: Structured telemetry data
- **Use case**: Performance analysis, distributed system debugging
- **Includes**: Complete metadata for trace correlation

## Best Practice

**Use both together:**
1. **Logs** provide immediate visibility during development and operations
2. **Traces** provide deep insights for performance analysis and distributed debugging

The current implementation gives you the best of both worlds:
- Key information in logs for quick debugging
- Complete telemetry in traces for deep analysis

## Example: Full Output

When you run the sample, you'll see:

**Structured Logs:**
```
info: PoliNorError.Extensions.Http.PolicyHttpMessageHandler[0]
      HTTP request succeeded after 245ms. Method=GET, Url=https://catfact.ninja/fact, Policy=RetryPolicy, StatusCode=200, TraceId=abc123..., SpanId=012ghi...
```

**OpenTelemetry Trace Export:**
```
Activity.TraceId:            abc123def456789...
Activity.SpanId:             012ghi345jkl...
Activity.DisplayName:        HttpPolicyExecution
Activity.Kind:               Client
Activity.Duration:           00:00:00.2450000
Activity.Tags:
    http.url: https://catfact.ninja/fact
    http.method: GET
    http.status_code: 200
    policy.type: RetryPolicy
    policy.is_final: True
    http.success: True
Activity.StatusCode:         Ok
```

Both show the same request, but with different levels of detail for different purposes.
