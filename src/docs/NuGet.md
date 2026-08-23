The library provides an outgoing request resiliency pipeline for `HttpClient`, using policies from the [PoliNorError](https://github.com/kolan72/PoliNorError) library.

## ⚡ Key Features

- **Explicit resiliency pipeline** based on `DelegatingHandler`s
---
- **Works with**
	- Typed and named `HttpClient`
  	- `IHttpClientFactory`
---
- **Flexible policy creation**
	- Inline policies
	- Policies resolved from `IServiceProvider`
	- Context-aware policy creation
---
- **Powerful final-handler failure filtering**
	- Precisely control *which* HTTP responses and exceptions should be treated as failures: 
		- Transient HTTP errors (5xx, 408, 429)
		- `HttpRequestException`
		- Custom status codes or status code categories
---
- **Full exception transparency** 
	- Failures are surfaced via a single, rich exception `HttpPolicyResultException`, preserving: 
		- The original exception
  		- HTTP response details
    	- Policy execution results
---
- **Control exception flow between handlers using `IncludeException<TException>`**
---
- **Deep PoliNorError integration** 
	- Use PoliNorError's fluent APIs for: 
		- Retry, fallback, and custom policies
  		- Exception filtering and processing
  		- Policy result inspection and logging
---
- **OpenTelemetry integration**
	- Built-in distributed tracing via `System.Diagnostics.ActivitySource`
	- Zero-cost when no listener is attached
		- Tags: `polinorerror.pipeline.result`, `polinorerror.pipeline.policy.type`, `polinorerror.pipeline.policy.name`, `polinorerror.pipeline.is_final_handler`, `http.request.method`, `url.full`, `url.path`, `server.address`, `http.response.status_code`
---
 - **.NET Standard 2.0 compatible**  
---

## 🔑 Key Concepts

- 🟦 **Resiliency pipeline**  - the pipeline of `DelegatingHandler`, using policies from the `PoliNorError` library.
- ➡ **OuterHandler** is the **first** handler in the pipeline (closest to the request initiator).
- ⬅ **InnerHandler** is the **next** handler in the pipeline (closer to the final destination).
- 🔵 **FinalHandler** is the innermost handler in the pipeline.
- ❌ **Transient HTTP errors** are temporary failures that occur when making HTTP requests (HTTP 5xx, HTTP 408, HTTP 429 and `HttpRequestException`). 

## 🚀 Usage

1. Configure  typed or named `HttpClient`:
```csharp
services.AddHttpClient<IAskCatService, AskCatService>((sp, config) =>
	{
		...
		config.BaseAddress = new Uri(settings.BaseUri);
		...
	})...
```
, where `AskCatService` is a service that implements `IAskCatService`, with `HttpClient` or `IHttpClientFactory` injected.

2. Use the library's `IHttpClientBuilder.WithResiliencePipeline` extension method to build a pipeline of `DelegatingHandler`s. Within this scope, configure a handler to use a policy via the `AddPolicyHandler` method:
```csharp
services.AddHttpClient<IAskCatService, AskCatService>((spForClient, client) =>
	{
			...
	})
	.WithResiliencePipeline((pb) => 
		pb
		.AddPolicyHandler(PolicyJustCreated)
		.AddPolicyHandler((IServiceProvider sp) => funcThatUsesServiceProviderToCreatePolicy(sp))
		...
	)
```
Or use the `WithResiliencePipeline` method overload that includes an additional context parameter:
```csharp
services.AddHttpClient<IAskCatService, AskCatService>((spForClient, client) =>
	{
			...
	})
	.WithResiliencePipeline<SomeContextType>((pb) => 
		pb
		.AddPolicyHandler((SomeContextType ctx, IServiceProvider sp) => 
			funcThatUsesContextAndServiceProviderToCreatePolicy(ctx, sp))

		.AddPolicyHandler((IServiceProvider sp) => funcThatUsesServiceProviderToCreatePolicy(sp))
		...
	, context)
```
, where   
- `pb` - represents the pipeline builder.
- `PolicyJustCreated` - a policy from the [PoliNorError](https://github.com/kolan72/PoliNorError) library.
- `funcThatUsesServiceProviderToCreatePolicy` - `Func` that uses the `IServiceProvider` to create a policy.  
- `funcThatUsesContextAndServiceProviderToCreatePolicy` - `Func` that uses the `IServiceProvider` and context to create a policy.  

3. Complete the pipeline by calling `AsFinalHandler` on the last handler and configuring `HttpErrorFilter` to filter transient HTTP errors,
```csharp
services.AddHttpClient<IAskCatService, AskCatService>((sp, config) =>
	{
			...
	})
	.WithResiliencePipeline((pb) => 
		pb
		...
		.AddPolicyHandler(PolicyForFinalHandler)
		//Adds transient http errors to the response handling filter.
		.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
		...
	)
```
and/or any non-successful status codes or categories
```csharp
		...
		.AsFinalHandler(HttpErrorFilter.HandleHttpRequestException()
			//Also adds 5XX status codes to the response handling filter.
			.OrServerError())
		...

```
Use `IncludeException<TException>` on the pipeline builder to allow an outer handler to handle only filtered exceptions from an inner handler or outside the pipeline:
```csharp
		...
		.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
		//Include 'SomeExceptionFromNonPipelineHandler' exceptions in the filter 
		//when thrown by a non-pipeline handler (in this case).
		.IncludeException<SomeExceptionFromNonPipelineHandler>()
		...

```  

4. Wrap `HttpClient` calls in a `catch` block for `HttpPolicyResultException`.
For unsuccessful requests, inspect the properties of `HttpPolicyResultException` to access response details:
```csharp
try
{
	...
	using var response = await _client.GetAsync(uri, token);
	...
}
catch (OperationCanceledException oe)
{
	...
}
catch (HttpPolicyResultException hpre)
{
	//If the response status code matches the handling filter status code:
	if (hpre.HasFailedResponse)
	{
		//For example, log a failed status code.
		logger.LogError("Failed status code: {StatusCode}.", hpre.FailedResponseData.StatusCode);
	}
}
catch (Exception ex)
{
	...
}
```

## 🔁 Adding Handlers Based on `RetryPolicy` Using the `AddRetryHandler` Extension Methods.

The `AddRetryHandler` extension methods provide a fluent way to attach a `RetryPolicy` to an HTTP message handler pipeline. 
One of these methods allows adding a handler via `RetryPolicyOptions` and is responsible for setting up `RetryPolicy` details, including:
- Error processing,
- Policy result handling,
- Error filters,
- Policy naming,
- Delay between retries,
- And ultimately registering the policy with `AddPolicyHandler`.

### Example: Retry with logging, filtering, and delay:
```csharp
var retryOptions = new RetryPolicyOptions()
{
	PolicyName = "MyRetryPolicy",

	ConfigureErrorProcessing = (bp) =>
		bp.WithErrorProcessorOf(
			(Exception ex, ProcessingErrorInfo pi) =>
				loggerTest.LogError(
					ex, 
					"Exception on attempt { Attempt }:", 
					pi.GetRetryCount() + 1)),

	ConfigureErrorFilter = (f) => f.ExcludeError<SomeException>(),

	ConfigurePolicyResultHandling = (handlers) => handlers.AddHandler(
			(pr, _) =>
			{
				if (pr.IsFailed)
				{
					loggerTest.LogWarning(
						"{Errors} exceptions were thrown during handling by {PolicyName}.",
						pr.Errors.Count(),
						pr.PolicyName);
				}
			}
		),
		
	RetryDelay = ConstantRetryDelay.Create(TimeSpan.FromSeconds(1))	
};
```
This example configures `RetryPolicyOptions` with:

- A policy name ("MyRetryPolicy"),
- An error processor (logs exceptions with attempt numbers),
- An error filter (excludes `SomeException`),
- A result handler (logs warnings about exception counts),
- A 1-second constant delay between retries.

Attach a retry handler to the pipeline using these options:
```csharp
services.AddHttpClient<IAskCatService, AskCatService>((sp, config) =>
	{
			...
	})
	.WithResiliencePipeline((pb) => 
		pb
		...
		//Maximum number of retries: 3  
		.AddRetryHandler(3, retryOptions)
		.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
		...
	)
```
You can also configure `RetryPolicy` details inline using the `AddRetryHandler` overload that accepts an `Action<RetryPolicyOptions>`.

## 🌡️ OpenTelemetry Integration

The library emits distributed-tracing activities via `System.Diagnostics.ActivitySource`. Each handler in the pipeline creates an `Activity` that records the policy execution result.

**Activity tags:**
- `polinorerror.pipeline.result` — `"success"` (policy succeeded), `"failed"` (policy returned a failed result), `"canceled"` (operation was canceled), or `"faulted"` (unexpected exception escaped the policy)
- `polinorerror.pipeline.policy.type` — PoliNorError policy type name (e.g. `RetryPolicy`, `FallbackPolicy`)
- `polinorerror.pipeline.policy.name` — the user-configured policy name, emitted only when explicitly set via `WithPolicyName`
- `polinorerror.pipeline.is_final_handler` — `true` if this handler is the final (response-classifying) handler
- `http.request.method` — HTTP request method (e.g. `GET`, `POST`), per [OTel HTTP semantic conventions](https://opentelemetry.io/docs/specs/semconv/http/http-spans/)
- `url.full` — absolute request URL; sensitive query parameters (`sig`, `X-Amz-Signature`, etc.) are redacted to `REDACTED`
- `url.path` — request path component
- `server.address` — server domain name or IP from the request URI
- `http.response.status_code` — HTTP response status code as an integer (e.g. `200`, `504`); emitted on the success path and on the final handler's activity when the response status was filtered

**Connecting to OpenTelemetry:**
```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("PoliNorError.Extensions.Http")
    .AddOtlpExporter()   // or AddConsoleExporter(), AddZipkinExporter(), etc.
    .Build();
```

No additional configuration is needed in the pipeline itself. The `ActivitySource` is zero-cost when no listener is attached.

## 📜 `HttpPolicyResultException` properties

Public properties of the `HttpPolicyResultException`:

- `InnerException` 
	- If the response status code matches the handling filter's status code, it will be a special `FailedHttpResponseException`.  
	- If no handlers inside or outside the resiliency pipeline throw an exception, and the `HttpClient`'s primary handler throws an `HttpRequestException`, the `InnerException` will be that `HttpRequestException`.
	- Otherwise, the exception originates from one of the handlers, either inside or outside the resiliency pipeline.
- `FailedResponseData` - not null if the status code part of the handling filter matches the response status code.
- `HasFailedResponse` - true if `FailedResponseData` is not null.
- `PolicyResult` - specifies the `PolicyResult<HttpResponseMessage>` result that is produced by a policy that belongs to the `DelegatingHandler` that throws this exception.  
- `InnermostPolicyResult` - specifies the `PolicyResult<HttpResponseMessage>` result produced by a policy of the final handler or by a handler in the pipeline that throws its own exception. 
- `IsErrorExpected` - indicates whether the filter for the original exception was satisfied.
- `IsCanceled` - indicates whether the execution was canceled.

## ❓ Why PoliNorError.Extensions.Http?

- **Declarative pipeline builder for `HttpClient` via `WithResiliencePipeline`**

- **First-class support for typed and named `HttpClient`**

- **You decide what a failure is** 
		- Filter transient HTTP errors in the flexible final handler and control exception flow between handlers.

- **One clear failure signal** 
		- All handled failures surface as a single, information-rich `HttpPolicyResultException`.

- **Helpers to add handlers with rich configuration (`AddRetryHandler`, `AddFallbackHandler`)**

- **First-class PoliNorError integration** 
		- Advanced error processing, contextual logging, and policy result inspection.

- **Built-in OpenTelemetry tracing**
		- Observe retry/fallback behavior in your distributed tracing backend with zero pipeline configuration.

##  Samples

See the samples folder for concrete examples.
