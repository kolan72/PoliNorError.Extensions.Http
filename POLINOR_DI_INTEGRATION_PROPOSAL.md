# PoliNorError.Extensions.DependencyInjection Integration Proposal

## Executive Summary

After analyzing the `PoliNorError.Extensions.DependencyInjection` package, I can confirm it provides an **elegant, type-safe DI pattern** for policy management. However, there's a **critical challenge**: the package is designed for **direct policy execution** (`IPolicy<T>.HandleAsync()`), while `PoliNorError.Extensions.Http` requires **policies to be passed to `AddPolicyHandler()`** for HTTP pipeline integration.

## Architecture Analysis

### How PoliNorError.Extensions.DependencyInjection Works

```
┌─────────────────────────────────────────────────────────────────┐
│                         DI Container                             │
├─────────────────────────────────────────────────────────────────┤
│  IPolicyBuilder<T> implementations (auto-discovered)            │
│  IPolicy<T> → ProxyPolicy<T> (open generic registration)       │
│  PolicyConfigurator<TPolicy> (optional, for advanced scenarios) │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                    Consumer injects IPolicy<T>
                              ↓
                  Calls IPolicy<T>.HandleAsync(func)
```

**Key Components:**

1. **`IPolicyBuilder<TBuilder>`** - Interface for building policies
   - Implements `Build()` method that returns `IPolicyBase`
   - Receives dependencies via constructor injection
   - Auto-discovered and registered by `AddPoliNorError()`

2. **`IPolicy<TBuilder>`** - Interface for consuming policies
   - Extends `IPolicyBase`
   - Type-safe wrapper around a specific builder
   - Resolved from DI as `ProxyPolicy<TBuilder>`

3. **`ProxyPolicy<TBuilder>`** - Internal implementation
   - Calls `IPolicyBuilder<TBuilder>.Build()` on construction
   - Delegates all `Handle`/`HandleAsync` calls to the built policy

4. **`PolicyConfigurator<TPolicy>`** - Optional advanced pattern
   - Separates policy creation from configuration
   - Reusable across multiple builders

### How PoliNorError.Extensions.Http Works

```
┌─────────────────────────────────────────────────────────────────┐
│                    HTTP Client Pipeline                          │
├─────────────────────────────────────────────────────────────────┤
│  HttpClient                                                      │
│    → DelegatingHandler (PolicyHttpMessageHandler)               │
│        → Wraps IPolicyBase                                       │
│        → Calls policy.HandleAsync(SendCoreAsync)                 │
└─────────────────────────────────────────────────────────────────┘
```

**Key Methods:**

```csharp
// Current approach - static class
.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(logger))

// Current approach - IServiceProvider lambda
.AddPolicyHandler((IServiceProvider sp) => 
{
    var logger = sp.GetRequiredService<ILogger>();
    return CatPolicies.GetFinalHandlerRetryPolicy(logger);
})
```

## The Challenge

**The DI package provides `IPolicy<T>` for direct execution, but HTTP pipeline needs `IPolicyBase` instances to wrap in `PolicyHttpMessageHandler`.**

### Why Direct Integration Doesn't Work

```csharp
// ❌ This won't work - IPolicy<T> is meant for direct execution
.AddPolicyHandler((IServiceProvider sp) =>
{
    var policy = sp.GetRequiredService<IPolicy<OuterRetryPolicyBuilder>>();
    return policy; // IPolicy<T> extends IPolicyBase, but...
})
```

**Problem:** `IPolicy<T>` is a **proxy** that wraps the built policy. When you pass it to `AddPolicyHandler()`, you're wrapping a proxy in another handler, creating unnecessary indirection.

## Proposed Solutions

### Solution 1: Use IPolicyBuilder<T> Directly (Recommended)

**Concept:** Resolve the builder from DI and call `Build()` to get the raw `IPolicyBase`.

```csharp
services
    .AddPoliNorError(Assembly.GetExecutingAssembly())
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder) => emptyBuilder
        .AddPolicyHandler((IServiceProvider sp) =>
        {
            var builder = sp.GetRequiredService<IPolicyBuilder<OuterRetryPolicyBuilder>>();
            return builder.Build();
        })
        .AddPolicyHandler((IServiceProvider sp) =>
        {
            var builder = sp.GetRequiredService<IPolicyBuilder<FinalRetryPolicyBuilder>>();
            return builder.Build();
        })
        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
```

**Pros:**
- ✅ Clean separation: builders for HTTP, `IPolicy<T>` for direct execution
- ✅ No wrapper overhead
- ✅ Type-safe
- ✅ Testable (mock `IPolicyBuilder<T>`)

**Cons:**
- ⚠️ Slightly verbose (need to call `.Build()`)
- ⚠️ Builders are called on every HTTP client creation (but policies are lightweight)

---

### Solution 2: Create Extension Methods for HTTP Integration

**Concept:** Add extension methods to `PoliNorError.Extensions.Http` that integrate with the DI package.

```csharp
// New extension method
public static class HttpPolicyBuilderExtensions
{
    public static IPolicyHandlerStorage AddPolicyHandler<TBuilder>(
        this IPolicyHandlerStorage storage)
        where TBuilder : IPolicyBuilder<TBuilder>
    {
        return storage.AddPolicyHandler((IServiceProvider sp) =>
        {
            var builder = sp.GetRequiredService<IPolicyBuilder<TBuilder>>();
            return builder.Build();
        });
    }
}
```

**Usage:**
```csharp
services
    .AddPoliNorError(Assembly.GetExecutingAssembly())
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder) => emptyBuilder
        .AddPolicyHandler<OuterRetryPolicyBuilder>()
        .AddPolicyHandler<FinalRetryPolicyBuilder>()
        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
```

**Pros:**
- ✅ Very clean and concise
- ✅ Type-safe
- ✅ Hides the `.Build()` call
- ✅ Consistent with existing API

**Cons:**
- ⚠️ Requires adding a package reference to `PoliNorError.Extensions.DependencyInjection`
- ⚠️ Couples the HTTP library to the DI library

---

### Solution 3: Hybrid Approach (Best of Both Worlds)

**Concept:** Keep both approaches - use builders for HTTP, use `IPolicy<T>` for direct execution.

**For HTTP Clients:**
```csharp
services
    .AddPoliNorError(Assembly.GetExecutingAssembly())
    .AddCatHttpClient()
    .WithResiliencePipeline((emptyBuilder) => emptyBuilder
        .AddPolicyHandler<OuterRetryPolicyBuilder>()  // Extension method
        .AddPolicyHandler<FinalRetryPolicyBuilder>()
        .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
```

**For Direct Execution:**
```csharp
public class SomeService
{
    private readonly IPolicy<DatabaseRetryPolicyBuilder> _dbPolicy;

    public SomeService(IPolicy<DatabaseRetryPolicyBuilder> dbPolicy)
    {
        _dbPolicy = dbPolicy;
    }

    public async Task DoWorkAsync()
    {
        await _dbPolicy.HandleAsync(async ct =>
        {
            // Database operation
        });
    }
}
```

**Pros:**
- ✅ Best tool for each job
- ✅ Clear separation of concerns
- ✅ Maximum flexibility

**Cons:**
- ⚠️ Two patterns to learn (but each is simple)

---

## Recommended Implementation Plan

### Phase 1: Add Extension Method to PoliNorError.Extensions.Http

**File:** `src/HttpPolicyBuilderExtensions.cs`

```csharp
#if NETSTANDARD2_0_OR_GREATER || NET6_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;

namespace PoliNorError.Extensions.Http
{
    /// <summary>
    /// Extension methods for integrating PoliNorError.Extensions.DependencyInjection
    /// with HTTP client resilience pipelines.
    /// </summary>
    public static class HttpPolicyBuilderExtensions
    {
        /// <summary>
        /// Adds a policy handler to the pipeline by resolving an IPolicyBuilder&lt;TBuilder&gt;
        /// from the service provider and calling Build().
        /// </summary>
        /// <typeparam name="TBuilder">
        /// The policy builder type that implements IPolicyBuilder&lt;TBuilder&gt;.
        /// Must be registered via AddPoliNorError().
        /// </typeparam>
        /// <param name="storage">The policy handler storage.</param>
        /// <returns>The policy handler storage for method chaining.</returns>
        /// <remarks>
        /// This method requires the PoliNorError.Extensions.DependencyInjection package
        /// and that AddPoliNorError() has been called to register policy builders.
        /// </remarks>
        public static IPolicyHandlerStorage AddPolicyHandler<TBuilder>(
            this IPolicyHandlerStorage storage)
            where TBuilder : class
        {
            return storage.AddPolicyHandler((IServiceProvider sp) =>
            {
                // Use dynamic to avoid hard dependency on DI package
                dynamic builder = sp.GetRequiredService(typeof(TBuilder));
                return (IPolicyBase)builder.Build();
            });
        }
    }
}
#endif
```

**Note:** Using `dynamic` avoids a hard package reference while still providing the functionality.

### Phase 2: Create Sample Policy Builders

**File:** `samples/Shared/Policies/OuterRetryPolicyBuilder.cs`

```csharp
using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.DependencyInjection;
using PoliNorError.Extensions.Http;

namespace Shared.Policies
{
    public class OuterRetryPolicyBuilder : IPolicyBuilder<OuterRetryPolicyBuilder>
    {
        private readonly ILogger<OuterRetryPolicyBuilder> _logger;

        public OuterRetryPolicyBuilder(ILogger<OuterRetryPolicyBuilder> logger)
        {
            _logger = logger;
        }

        public IPolicyBase Build()
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
                .AddPolicyResultHandler<HttpResponseMessage>(pr =>
                {
                    if (pr.UnprocessedError is not null)
                    {
                        _logger.LogError(pr.UnprocessedError,
                            "UnprocessedError in {PolicyName}",
                            policyName);
                    }
                });
        }
    }
}
```

**File:** `samples/Shared/Policies/FinalRetryPolicyBuilder.cs`

```csharp
using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.DependencyInjection;
using PoliNorError.Extensions.Http;

namespace Shared.Policies
{
    public class FinalRetryPolicyBuilder : IPolicyBuilder<FinalRetryPolicyBuilder>
    {
        private readonly ILogger<FinalRetryPolicyBuilder> _logger;

        public FinalRetryPolicyBuilder(ILogger<FinalRetryPolicyBuilder> logger)
        {
            _logger = logger;
        }

        public IPolicyBase Build()
        {
            const string policyName = "FinalHandlerAskCatRetryPolicy";
            return new RetryPolicy(3)
                .WithPolicyName(policyName)
                .WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
                {
                    _logger.LogError(ex,
                        "Policy {PolicyName} handled exception on attempt {Attempt}",
                        policyName,
                        pi.GetRetryCount() + 1);
                    
                    if (ex is FailedHttpResponseException failedException)
                    {
                        _logger.LogWarning(ex,
                            "Cat answer error. StatusCode {StatusCode}",
                            failedException.FailedResponseData.StatusCode);
                    }
                })
                .AddPolicyResultHandler<HttpResponseMessage>(pr =>
                {
                    if (pr.IsPolicySuccess)
                        _logger.LogInformation("Policy {PolicyName} succeeded", pr.PolicyName);
                    else if (pr.IsFailed)
                    {
                        _logger.LogWarning("{Errors} exceptions in {PolicyName}",
                            pr.Errors.Count(),
                            pr.PolicyName);
                    }
                })
                .WithWait(TimeSpan.FromMilliseconds(1000));
        }
    }
}
```

### Phase 3: Update Sample to Use DI Pattern

**File:** `samples/FromOptions/RetryFromOptions/Program.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError.Extensions.DependencyInjection;
using PoliNorError.Extensions.Http;
using Shared;
using Shared.Policies;
using System.Reflection;

namespace RetryFromOptions
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var services = new ServiceCollection();

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Register policy builders via DI
            services.AddPoliNorError(
                typeof(Program).Assembly,           // Current assembly
                typeof(CatPolicies).Assembly);      // Shared assembly

            services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

            _ = services
                .AddConfig()
                .AddCatHttpClient()
                .WithResiliencePipeline((emptyBuilder) => emptyBuilder
                    // Use policy builders from DI
                    .AddPolicyHandler<OuterRetryPolicyBuilder>()
                    .AddPolicyHandler<FinalRetryPolicyBuilder>()
                    .AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()))
                .AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

            UtilsConsole.PrintHello();

            await using (var provider = services.BuildServiceProvider())
            {
                var service = provider.GetRequiredService<IAskCatService>();
                await CatFactManager.GetCatFactOnRetry(service, provider.GetRequiredService<ILogger<Program>>());
            }

            UtilsConsole.PrintBye();
        }
    }
}
```

---

## Benefits of This Approach

### 1. Eliminates Static Class ✅
- No more `CatPolicies` static class
- All policies managed through DI

### 2. Type-Safe ✅
- Compile-time checking
- No string-based lookups

### 3. Testable ✅
```csharp
var mockBuilder = new Mock<IPolicyBuilder<OuterRetryPolicyBuilder>>();
mockBuilder.Setup(b => b.Build()).Returns(new RetryPolicy(1));
```

### 4. Flexible ✅
- Different implementations per environment
- Easy to override for testing

### 5. Clean Code ✅
```csharp
// Before
.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(loggerTest))

// After
.AddPolicyHandler<OuterRetryPolicyBuilder>()
```

### 6. Separation of Concerns ✅
- Policy creation logic in builders
- HTTP pipeline configuration separate
- Logging injected automatically

---

## Migration Strategy

### Step 1: Add Package Reference
```xml
<PackageReference Include="PoliNorError.Extensions.DependencyInjection" Version="0.0.3.2" />
```

### Step 2: Create Policy Builders
- Move logic from `CatPolicies` static methods to builder classes
- One builder per policy

### Step 3: Add Extension Method (Optional)
- Add `AddPolicyHandler<TBuilder>()` to `PoliNorError.Extensions.Http`
- Or use the verbose approach with `.Build()`

### Step 4: Update Samples
- Update one sample first (e.g., `RetryFromOptions`)
- Verify it works
- Update remaining samples

### Step 5: Deprecate Static Class
- Mark `CatPolicies` as `[Obsolete]`
- Remove in next major version

---

## Comparison: Before vs After

| Aspect | Static Class | DI Builders |
|--------|-------------|-------------|
| **Registration** | None | `AddPoliNorError()` |
| **Usage** | `CatPolicies.Get...()` | `AddPolicyHandler<T>()` |
| **Dependencies** | Manual passing | Auto-injected |
| **Testability** | Hard to mock | Easy to mock |
| **Type Safety** | ✅ | ✅ |
| **Verbosity** | Medium | Low |
| **Flexibility** | Low | High |

---

## Recommendation

**Implement Solution 2 (Extension Methods) with the following approach:**

1. **Add the extension method** to `PoliNorError.Extensions.Http` (using `dynamic` to avoid hard dependency)
2. **Create policy builders** in `Shared/Policies/` folder
3. **Update one sample** to demonstrate the pattern
4. **Keep `CatPolicies`** for backward compatibility (mark as obsolete)
5. **Document the new pattern** in README

This provides the cleanest API while maintaining flexibility and avoiding breaking changes.

**Would you like me to implement this solution?**
