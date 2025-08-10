## 0.3.0

- Update lib and tests to PoliNorError 2.23.0.
- Introduced the `RetryPolicyOptions` and `PolicyOptions` classes.
- Added an `AddRetryHandler` overload to pipeline builders that accepts a `retryCount` and `RetryPolicyOptions`.
- Added an `AddRetryHandler` overload to pipeline builders that accepts a `retryCount` and `Action<RetryPolicyOptions>`.
- Introduced the `IHttpPolicyResultHandlers` interface.
- Update Microsoft NuGet packages.
- Update Microsoft NuGet packages for PoliNorError.Extensions.Http.Tests.
- Update the Microsoft packages and set PoliNorError to version 2.23.0 for the Samples.
- Switch to new way of getting current retry attempt for the Samples.


## 0.2.0

- Added `AddRetryHandler` method to the library's pipeline builders.
- Added an overload to the `AddRetryHandler` method that accepts a `Func<IServiceProvider, RetryPolicy>` factory delegate.
- Added an overload to the `AddRetryHandler` method that accepts a `Func<TContext, IServiceProvider, RetryPolicy>` factory delegate.
- Update lib to PoliNorError 2.22.0.
- Update Samples to PoliNorError 2.22.0.
- Update the Spectre.Console package in the Shared.csproj (Samples).
- Removed redundant Spectre.Console package from Retries.csproj (Samples).
- Update Microsoft NuGet packages.
- Add CHANGELOG.md.