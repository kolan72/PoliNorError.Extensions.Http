# PoliNorError.Extensions.Http

The library provides an outgoing request resiliency pipeline for `HttpClient`, using policies from the [PoliNorError](https://github.com/kolan72/PoliNorError) library.

![PoliNorError.Extensions.Http](PoliNorError.png)

![Pipeline](/src/docs/diagrams/Pipeline.png)

## ⚡ Key Features

- Provides the ability to create a pipeline to handle typical transient HTTP failures (including the `HttpRequestException` exception).  
- Flexible transient failure filter for the final `DelegatingHandler` in the pipeline for the response.  
- Additionally, custom failure status codes or categories can be added to the final handler filter.  
- Other exception types (besides `HttpRequestException`) can also be included in the final handler filter.  
- Inclusion in the outer handler filter of any `Exception` type thrown by the inner handler is also supported.  
- Both typed and named `HttpClient`, as well as `IHttpClientFactory`, can be used. 
- Targets .NET Standard 2.0.  

## 🔑 Key Concepts

- ➡ **OuterHandler** is the **first** handler in the pipeline (closest to the request initiator).
- ⬅ **InnerHandler** is the **next** handler in the pipeline (closer to the final destination).
- 🔵 **FinalHandler** is the innermost handler in the pipeline.
- ❌ **Transient HTTP errors** are temporary failures that occur when making HTTP requests (HTTP 5xx, HTTP 408, HTTP 429 and `HttpRequestException`). 

## 🚀 Usage

⚙ Configure  typed or named `HttpClient`:

```csharp
services.AddHttpClient<IAskCatService, AskCatService>((sp, config) =>
	{
		...
		config.BaseAddress = new Uri(settings.BaseUri);
		...
	})...
```
, where `AskCatService` is a service that implements `IAskCatService`, with `HttpClient` or `IHttpClientFactory` injected.

---
🧩 Use the library's `IHttpClientBuilder.WithResiliencePipeline` extension method and configure the pipeline of `DelegatingHandler`s by using the `AddPolicyHandler` method with the policy you want to apply in this handler:

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
---
🔵 When you want to complete the pipeline, call the `AsFinalHandler` method for the last added handler and configure `HttpErrorFilter` to filter transient http errors and/or any non-successful status codes or categories:

```csharp
services.AddHttpClient<IAskCatService, AskCatService>((sp, config) =>
	{
			...
	})
	.WithResiliencePipeline((pb) => 
		pb
		...
		.AddPolicyHandler(PolicyForFinalHandler)
		// ✔ Adds transient http errors to the response handling filter.
		.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
		...
	)
```
Additionally, you can include custom failure status codes or categories in the final handler filter:
```csharp
		...
		.AsFinalHandler(HttpErrorFilter.HandleHttpRequestException()
			// ✔ Also adds 5XX status codes to the response handling filter.
			.OrServerError())
		...

```
You can also include in the filter any exception type thrown by an inner handler:
```csharp
		...
		.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
		// ✔ Include 'SomeExceptionFromNonPipelineHandler' exceptions in the filter 
		//when thrown by a non-pipeline handler (in this case).
		.IncludeException<SomeExceptionFromNonPipelineHandler>()
		...

```
---
⚾ In a service that uses `HttpClient` or `HttpClientFactory`, wrap the call to `HttpClient` in a catch block that handles the special `HttpPolicyResultException` exception. 
If the request was not successful, examine the `HttpPolicyResultException` properties in this handler for details of the response:

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
	// ✔ If the response status code matches the handling filter status code:
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

The `AddRetryHandler` extension methods provide a fluent way to attach a `RetryPolicy` to an HTTP message handler pipeline. 
One of these methods allows adding a handler via `RetryPolicyOptions` and is responsible for setting up `RetryPolicy` details, including:
- Error processing,
- Policy result handling,
- Error filters,
- Policy naming,
- Delay between retries,
- And ultimately registering the policy with `AddPolicyHandler`.

For example:
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

This is how `RetryPolicyOptions` is passed to `AddRetryHandler` in the pipeline:
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

## 📜 `HttpPolicyResultException` properties

Public properties of the `HttpPolicyResultException`:

- `InnerException` 
	- if the response status code matches the handling filter status code, it will be the special `FailedHttpResponseException`;  
	- otherwise, an exception is thrown by a handler, either inside or outside the pipeline.
- `FailedResponseData` - not null if the status code part of the handling filter matches the response status code.
- `HasFailedResponse` - true if `FailedResponseData` is not null.
- `PolicyResult` - specifies the `PolicyResult<HttpResponseMessage>` result that is produced by a policy that belongs to the `DelegatingHandler` that throws this exception.  
- `InnermostPolicyResult` - specifies the `PolicyResult<HttpResponseMessage>` result produced by a policy of the final handler or by a handler in the pipeline that throws its own exception. 
- `IsErrorExpected` - indicates whether the filter for the original exception was satisfied.
- `IsCanceled` - indicates whether the execution was canceled.

## 🐈 Samples [![CSharp](https://img.shields.io/badge/C%23-code-blue.svg)](samples/Intro)

See the [/samples](samples/Intro) folder for concrete examples.

## 🔗 Links And Thanks

Steve Gordon. HttpClientFactory in ASP.NET Core 2.1 (Part 3) :  
https://www.stevejgordon.co.uk/httpclientfactory-aspnetcore-outgoing-request-middleware-pipeline-delegatinghandlers  

Martin Tomka. Building resilient cloud services with .NET 8 :
https://devblogs.microsoft.com/dotnet/building-resilient-cloud-services-with-dotnet-8/

Thomas Levesque. Fun with the HttpClient pipeline :
https://thomaslevesque.com/2016/12/08/fun-with-the-httpclient-pipeline/  

Milan Jovanovic. Extending HttpClient With Delegating Handlers in ASP.NET Core :  
https://www.milanjovanovic.tech/blog/extending-httpclient-with-delegating-handlers-in-aspnetcore  

Josef Ottosson. Testing your Polly policies :  
https://josef.codes/testing-your-polly-policies/  

