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