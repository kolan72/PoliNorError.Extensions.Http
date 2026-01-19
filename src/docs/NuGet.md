The library provides an outgoing request resiliency pipeline for `HttpClient`, using policies from the [PoliNorError](https://github.com/kolan72/PoliNorError) library.

## ⚡ Key Features

- Provides the ability to create a resiliency pipeline to handle typical transient HTTP failures (including the `HttpRequestException` exception).  
- Flexible transient failure filter for the final `DelegatingHandler` in the pipeline for the response.  
- Additionally, custom failure status codes or categories can be added to the final handler filter.  
- Other exception types (besides `HttpRequestException`) can also be included in the final handler filter.  
- Inclusion in the outer handler filter of any `Exception` type thrown by the inner handler is also supported.  
- Both typed and named `HttpClient`, as well as `IHttpClientFactory`, can be used.  
- Targets .NET Standard 2.0.  

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

2. Use the library's `IHttpClientBuilder.WithResiliencePipeline` extension method and configure the pipeline of `DelegatingHandler`s by using the `AddPolicyHandler` method with the policy you want to apply in this handler:
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
You can also include in the filter any exception type thrown by an inner handler:
```csharp
		...
		.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
		//Include 'SomeExceptionFromNonPipelineHandler' exceptions in the filter 
		//when thrown by a non-pipeline handler (in this case).
		.IncludeException<SomeExceptionFromNonPipelineHandler>()
		...

```  

4. In a service that uses `HttpClient` or `HttpClientFactory`, wrap the call to `HttpClient` in a catch block that handles the special `HttpPolicyResultException` exception. 
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

## 📜 `HttpPolicyResultException` properties

Public properties of the `HttpPolicyResultException`:

- `InnerException` 
	- If the response status code matches the handling filter’s status code, it will be a special `FailedHttpResponseException`.  
	- If no handlers inside or outside the resiliency pipeline throw an exception, and the `HttpClient`’s primary handler throws an `HttpRequestException`, the `InnerException` will be that `HttpRequestException`.
	- Otherwise, the exception originates from one of the handlers, either inside or outside the pipeline.
- `FailedResponseData` - not null if the status code part of the handling filter matches the response status code.
- `HasFailedResponse` - true if `FailedResponseData` is not null.
- `PolicyResult` - specifies the `PolicyResult<HttpResponseMessage>` result that is produced by a policy that belongs to the `DelegatingHandler` that throws this exception.  
- `InnermostPolicyResult` - specifies the `PolicyResult<HttpResponseMessage>` result produced by a policy of the final handler or by a handler in the pipeline that throws its own exception. 
- `IsErrorExpected` - indicates whether the filter for the original exception was satisfied.
- `IsCanceled` - indicates whether the execution was canceled.

##  Samples

See the samples folder for concrete examples.