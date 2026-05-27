# Retry with Dependency Injection Sample

This sample demonstrates how to use **Dependency Injection** with policy builders for HTTP resilience in the Cat API client.

## What This Sample Shows

- **Policy Builders with DI**: Using `IPolicyBuilder<T>` implementations instead of static factory methods
- **Automatic Policy Discovery**: Using `AddPoliNorError()` to scan assemblies and register policy builders
- **Shared Logger**: Both policy builders receive the same `ILogger` instance through DI
- **Clean API**: Resolving policies from the service provider in the HTTP pipeline configuration

## Key Components

### Policy Builders

Located in `samples/Shared/Policies/`:

- **OuterRetryPolicyBuilder**: Outer retry policy with 2 retries and 3-second delay
- **FinalRetryPolicyBuilder**: Final retry policy with 3 retries and 1-second delay

### Registration

```csharp
// Register policy builders via DI
services.AddPoliNorError(
    typeof(CatPolicies).Assembly);  // Scans Shared assembly for IPolicyBuilder implementations
```

### Usage in HTTP Pipeline

```csharp
services
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder) =>
    {
        return emptyBuilder
            // Resolve policy builders from DI
            .AddPolicyHandler((IServiceProvider sp) =>
            {
                var builder = sp.GetRequiredService<IPolicyBuilder<OuterRetryPolicyBuilder>>();
                return (RetryPolicy)builder.Build();
            })
            .AddPolicyHandler((IServiceProvider sp) =>
            {
                var builder = sp.GetRequiredService<IPolicyBuilder<FinalRetryPolicyBuilder>>();
                return (RetryPolicy)builder.Build();
            })
            .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
    });
```

## Benefits Over Static Factory Methods

1. **Dependency Injection**: Policies receive dependencies (like `ILogger`) through constructor injection
2. **Testability**: Easy to mock `IPolicyBuilder<T>` for unit testing
3. **Type Safety**: Compile-time checking of policy types
4. **Separation of Concerns**: Policy creation logic is isolated in dedicated builder classes
5. **Shared Resources**: Multiple policies can share the same logger or other services

## Running the Sample

```bash
cd samples/WithDI/RetryWithDI
dotnet run
```

The sample will:
1. Make HTTP requests to the Cat Facts API
2. Apply retry policies when transient errors occur
3. Log all retry attempts and policy results
4. Display cat facts or fallback messages

## See Also

- [Policy Builders Documentation](../../Shared/Policies/README.md)
- [PoliNorError.Extensions.DependencyInjection](https://www.nuget.org/packages/PoliNorError.Extensions.DependencyInjection)
- [Telemetry Documentation](../../../src/docs/Telemetry.md)
