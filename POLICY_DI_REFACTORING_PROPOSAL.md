# Policy Creation Refactoring Proposal: From Static Class to Dependency Injection

## Current State Analysis

### Problems with Current Approach

**1. Static `CatPolicies` Class**
```csharp
public static class CatPolicies
{
    public static RetryPolicy GetFinalHandlerRetryPolicy(ILogger logger) { ... }
    public static RetryPolicy GetOuterRetryPolicy(ILogger logger) { ... }
    public static FallbackPolicyBase GetOuterFallbackPolicy(ILogger logger) { ... }
}
```

**Issues:**
- ❌ Not testable (static methods are hard to mock)
- ❌ Tight coupling to `ILogger` parameter passing
- ❌ No lifecycle management
- ❌ Violates Dependency Inversion Principle
- ❌ Cannot leverage DI container benefits
- ❌ Difficult to override or customize per environment

**2. Manual Logger Passing**
```csharp
.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(loggerTest))
.AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(loggerTest))
```

**Issues:**
- ❌ Logger must be manually passed every time
- ❌ Verbose and repetitive
- ❌ Easy to forget or pass wrong logger

---

## Proposed Solution: Policy Factory Pattern with DI

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    DI Container                              │
├─────────────────────────────────────────────────────────────┤
│  ILogger<T>                                                  │
│  ICatPolicyFactory  ──────────────────────────────────────> │
│  CatPolicyFactory (implements ICatPolicyFactory)            │
│  HttpClient (with policies from factory)                    │
└─────────────────────────────────────────────────────────────┘
```

### Implementation

#### 1. Define Policy Factory Interface

```csharp
namespace Shared.Policies
{
    /// <summary>
    /// Factory for creating resilience policies for Cat API HTTP client.
    /// </summary>
    public interface ICatPolicyFactory
    {
        /// <summary>
        /// Creates the outer retry policy for the Cat API client.
        /// </summary>
        RetryPolicy CreateOuterRetryPolicy();

        /// <summary>
        /// Creates the final handler retry policy for the Cat API client.
        /// </summary>
        RetryPolicy CreateFinalHandlerRetryPolicy();

        /// <summary>
        /// Creates the outer fallback policy with default fallback response.
        /// </summary>
        FallbackPolicyBase CreateOuterFallbackPolicy();

        /// <summary>
        /// Creates the outer fallback policy with custom fallback response.
        /// </summary>
        FallbackPolicyBase CreateOuterFallbackPolicy(string customAnswer);
    }
}
```

#### 2. Implement Policy Factory

```csharp
namespace Shared.Policies
{
    /// <summary>
    /// Default implementation of ICatPolicyFactory.
    /// </summary>
    public class CatPolicyFactory : ICatPolicyFactory
    {
        private readonly ILogger<CatPolicyFactory> _logger;

        public CatPolicyFactory(ILogger<CatPolicyFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RetryPolicy CreateOuterRetryPolicy()
        {
            const string policyName = "OuterAskCatRetryPolicy";
            return new RetryPolicy(2)
                .WithPolicyName(policyName)
                .WithErrorProcessorOf(ex =>
                {
                    _logger.LogError(ex,
                        "Policy {PolicyName} handled exception: {ExceptionMessage}",
                        policyName, ex.Message);
                })
                .WithErrorProcessorOf((_) =>
                    AnsiConsole.Status()
                        .Start("Cat needs a little rest...", _ => Thread.Sleep(3000))
                )
                .AddPolicyResultHandler<HttpResponseMessage>(pr =>
                {
                    if (pr.UnprocessedError is not null)
                    {
                        _logger.LogError(pr.UnprocessedError,
                            "UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
                            policyName);
                    }
                });
        }

        public RetryPolicy CreateFinalHandlerRetryPolicy()
        {
            const string policyName = "FinalHandlerAskCatRetryPolicy";
            return new RetryPolicy(3)
                .WithPolicyName(policyName)
                .WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
                {
                    _logger.LogError(ex,
                        "Policy {PolicyName} handled an exception on attempt {Attempt}:",
                        policyName,
                        pi.GetRetryCount() + 1);
                    if (ex is FailedHttpResponseException failedException)
                    {
                        _logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}",
                            failedException.FailedResponseData.StatusCode);
                    }
                })
                .AddPolicyResultHandler<HttpResponseMessage>(pr =>
                {
                    if (pr.IsPolicySuccess)
                        _logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
                    else if (pr.IsFailed)
                    {
                        _logger.LogWarning("{Errors} exceptions were thrown during handling by {PolicyName}.",
                            pr.Errors.Count(),
                            pr.PolicyName);
                        if (pr.UnprocessedError is not null)
                        {
                            _logger.LogError(pr.UnprocessedError,
                                "UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
                                policyName);
                        }
                    }
                })
                .WithWait(TimeSpan.FromMilliseconds(1000));
        }

        public FallbackPolicyBase CreateOuterFallbackPolicy()
        {
            return new FallbackPolicy()
                .WithPolicyName("CatAnswerFallbackPolicy")
                .WithAsyncFallbackFunc((_) => Task.FromResult(UsualFallbackCatAnswer))
                .AddPolicyResultHandler<HttpResponseMessage>(pr =>
                {
                    if (pr.IsPolicySuccess)
                        _logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
                });
        }

        public FallbackPolicyBase CreateOuterFallbackPolicy(string customAnswer)
        {
            return new FallbackPolicy()
                .WithPolicyName("CatAnswerFallbackPolicy")
                .WithAsyncFallbackFunc((_) => Task.FromResult(GetCustomFallbackCatAnswer(customAnswer)))
                .AddPolicyResultHandler<HttpResponseMessage>(pr =>
                {
                    if (pr.IsPolicySuccess)
                        _logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
                });
        }

        private static HttpResponseMessage GetCustomFallbackCatAnswer(string customAnswer)
            => new HttpResponseMessage()
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new CatResponse() { Fact = customAnswer }),
                    Encoding.UTF8,
                    "application/json")
            };

        private static HttpResponseMessage UsualFallbackCatAnswer
            => new HttpResponseMessage()
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new CatResponse() { Fact = "Meow!" }),
                    Encoding.UTF8,
                    "application/json")
            };
    }
}
```

#### 3. Register in DI Container

```csharp
// In ConfigServiceCollectionExtensions.cs or Program.cs
public static IServiceCollection AddCatPolicies(this IServiceCollection services)
{
    services.AddSingleton<ICatPolicyFactory, CatPolicyFactory>();
    return services;
}
```

#### 4. Use in HTTP Client Configuration

**Before (Static Class):**
```csharp
services
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder) =>
    {
        return emptyBuilder
            .AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(loggerTest))
            .AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(loggerTest))
            .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
    });
```

**After (DI Factory):**
```csharp
services
    .AddCatPolicies()  // Register policy factory
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder, sp) =>
    {
        var policyFactory = sp.GetRequiredService<ICatPolicyFactory>();
        return emptyBuilder
            .AddPolicyHandler(policyFactory.CreateOuterRetryPolicy())
            .AddPolicyHandler(policyFactory.CreateFinalHandlerRetryPolicy())
            .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
    });
```

**Even Better (Using existing IServiceProvider overload):**
```csharp
services
    .AddCatPolicies()
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder) => emptyBuilder
        .AddPolicyHandler((IServiceProvider sp) =>
        {
            var factory = sp.GetRequiredService<ICatPolicyFactory>();
            return factory.CreateOuterRetryPolicy();
        })
        .AddPolicyHandler((IServiceProvider sp) =>
        {
            var factory = sp.GetRequiredService<ICatPolicyFactory>();
            return factory.CreateFinalHandlerRetryPolicy();
        })
        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
```

---

## Benefits of DI Approach

### 1. Testability ✅
```csharp
// Easy to mock in unit tests
var mockFactory = new Mock<ICatPolicyFactory>();
mockFactory.Setup(f => f.CreateOuterRetryPolicy())
    .Returns(new RetryPolicy(1));
```

### 2. Flexibility ✅
```csharp
// Different implementations per environment
services.AddSingleton<ICatPolicyFactory, ProductionCatPolicyFactory>();
// or
services.AddSingleton<ICatPolicyFactory, DevelopmentCatPolicyFactory>();
```

### 3. Lifecycle Management ✅
```csharp
// Singleton: One instance shared across application
services.AddSingleton<ICatPolicyFactory, CatPolicyFactory>();

// Scoped: One instance per request (if needed)
services.AddScoped<ICatPolicyFactory, CatPolicyFactory>();

// Transient: New instance every time (if needed)
services.AddTransient<ICatPolicyFactory, CatPolicyFactory>();
```

### 4. Dependency Injection ✅
```csharp
// Factory can depend on other services
public class CatPolicyFactory : ICatPolicyFactory
{
    private readonly ILogger<CatPolicyFactory> _logger;
    private readonly IOptions<CatPolicyOptions> _options;
    private readonly IMetricsCollector _metrics;

    public CatPolicyFactory(
        ILogger<CatPolicyFactory> logger,
        IOptions<CatPolicyOptions> options,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _options = options;
        _metrics = metrics;
    }
}
```

### 5. Configuration-Driven ✅
```csharp
// appsettings.json
{
  "CatPolicyOptions": {
    "OuterRetryCount": 2,
    "FinalRetryCount": 3,
    "RetryDelayMs": 1000
  }
}

// Use in factory
public RetryPolicy CreateFinalHandlerRetryPolicy()
{
    var retryCount = _options.Value.FinalRetryCount;
    var delay = TimeSpan.FromMilliseconds(_options.Value.RetryDelayMs);
    
    return new RetryPolicy(retryCount)
        .WithWait(delay)
        // ...
}
```

### 6. Clean Code ✅
- No manual logger passing
- Clear separation of concerns
- Follows SOLID principles
- Easy to extend and maintain

---

## Migration Path

### Phase 1: Add Factory (Non-Breaking)
1. Create `ICatPolicyFactory` interface
2. Create `CatPolicyFactory` implementation
3. Keep `CatPolicies` static class for backward compatibility
4. Add extension method `AddCatPolicies()`

### Phase 2: Update Samples
1. Update one sample to use factory
2. Verify it works
3. Update remaining samples
4. Document the new approach

### Phase 3: Deprecate Static Class (Optional)
1. Mark `CatPolicies` as `[Obsolete]`
2. Update all samples to use factory
3. Remove static class in next major version

---

## Comparison Table

| Aspect | Static Class | DI Factory |
|--------|-------------|------------|
| **Testability** | ❌ Hard to mock | ✅ Easy to mock |
| **Flexibility** | ❌ Fixed implementation | ✅ Swappable implementations |
| **Dependencies** | ❌ Manual passing | ✅ Auto-injected |
| **Configuration** | ❌ Hardcoded | ✅ Configuration-driven |
| **Lifecycle** | ❌ No control | ✅ Full control |
| **SOLID** | ❌ Violates DIP | ✅ Follows SOLID |
| **Verbosity** | ⚠️ Medium | ✅ Low (after setup) |
| **Learning Curve** | ✅ Simple | ⚠️ Requires DI knowledge |

---

## Recommendation

**Implement the DI Factory pattern** for the following reasons:

1. **Modern .NET Best Practice**: DI is the standard approach in .NET Core/5+
2. **Better Architecture**: Follows SOLID principles and clean architecture
3. **Testability**: Critical for unit testing
4. **Flexibility**: Easy to customize per environment
5. **Maintainability**: Easier to extend and modify
6. **Already Supported**: The library already has `IServiceProvider` overloads

The migration can be done incrementally without breaking existing code, making it a low-risk, high-reward refactoring.

---

## Next Steps

1. **Create the factory interface and implementation** in `Shared` project
2. **Update one sample** (e.g., `RetryFromOptions`) to demonstrate the new approach
3. **Document the pattern** in README with examples
4. **Gradually migrate** other samples
5. **Consider deprecating** the static class in a future version

Would you like me to implement this refactoring?
