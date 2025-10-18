## 0.5.0

- Introduced the `RetryPolicyOptions.ProcessRetryAfterHeader` property, which allows handling of the `Retry-After` header.
- Add support for configuring non-cancelable policy result handlers in `PolicyOptions`.
- Refactor handler addition in `HttpPolicyResultHandlers`.
- Refactor `AddRetryHandler` overloads to construct `RetryPolicy` using `RetryPolicyOptions`.
- Update lib and tests to PoliNorError 2.24.0.
- Update Microsoft nuget packages.
- Upgrade Samples to PoliNorError v2.24.0.
- Update Microsoft NuGet packages for Samples.
- Add the RetryFromOptions project to Samples.sln to see an example of a retry handler built from options.
- Add tests for the `RetryPolicyOptions.ConfigureErrorFilter` method related to the outer handler.
- Added tests for the asynchronous policy result handler in `RetryPolicyOptions`.
- Replace synchronous disposal with `await using` in the Samples projects.


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