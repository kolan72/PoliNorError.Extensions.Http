# RetryWithOptions Sample

This sample demonstrates how to use `IHttpClientBuilder.AddResilienceHandler<TContext>()` extension method with context-based configuration using `IOptions<RetryOptions>`.

## Key Features

- **Context-Based Configuration**: Uses `RetryOptions` (loaded from `IOptions<RetryOptions>`) as the context parameter for `WithResiliencePipeline`
- **Configuration from appSettings.json**: Retry settings (max retry count, delay, policy name) are loaded from configuration
- **Type-Safe Options Pattern**: Leverages the ASP.NET Core options pattern for strongly-typed configuration
- **Dependency Injection**: Demonstrates how to access services from the `IServiceProvider` within policy handlers

## How It Works

1. **RetryOptions Configuration**:
   - Defined in `appSettings.json` with properties like `MaxRetryCount`, `DelayMilliseconds`, and `PolicyName`
   - Registered in DI container using `services.Configure<RetryOptions>()`

2. **Benefits**:
   - Retry behavior can be changed via configuration without code modifications
   - Type-safe access to configuration values
   - Easy to inject other dependencies from the service provider

## Configuration Structure

```json
{
  "RetryOptions": {
    "MaxRetryCount": 3,
    "DelayMilliseconds": 1000,
    "PolicyName": "ContextBasedRetryPolicy"
  }
}
```

## Comparison with Other Approaches

- **vs WithResiliencePipeline (no context)**: This approach passes `RetryOptions` as context, making configuration available to all policy handlers
- **vs RetryWithDI**: This focuses on configuration-driven approach rather than policy builder injection
- **vs RetryFromOptions**: This demonstrates how to pass options as context to the pipeline, allowing dynamic policy configuration based on the context object

## Running the Sample

```bash
cd WithDI/RetryWithOptions
dotnet run
```
