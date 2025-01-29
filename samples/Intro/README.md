# About my cat
![alt text](https://github.com/kolan72/kolan72.github.io/blob/master/images/my_spiritual_cat.png?raw=true)  

My cat is very spiritual. He has access to the Collective Unconscious (or Conscious, who knows?), at least the cat part.  
With this program you can ask my cat for a fact about cat life, and if he is in a good mood, he will give you one for the first time.   
But sometimes he's not online or doesn't feel like talking and just translates his bad mood nuances into http errors.  
To get an answer in this case, you can use a few tactics.


## 🐱 Retry

If the cat gives a wrong answer, you can ask again a maximum of 3 times with an interval of 1 second (it's the final handler made by `RetryPolicy`).  
After 2 such unsuccessful sessions (repeating is supported by the outer handler also created by the other `RetryPolicy`) with an interval of 3 seconds (cat needs a little rest), you will be asked if you want to continue.  
If you choose "yes", sooner or later the cat will answer correctly.  
Set the 'Retry' project as the start project and run it to see if it works.
Note that when constructing the final handler, we use the `AddPolicyHandler` method overload with the `IServiceProvider` parameter.
Note also that in the outer retry policy we wait between retries by error processor using the default spinner from the 'Spectre.Console' nuget package (may not work correctly on some terminals).  


## 😼 Fallback

Instead of trying to imitate a smart cat, you can suggest that he just say "meow" as a typical cat answer.
After all, it's just a cat most of the time.  
Set the 'Fallback' project as the start project and run it to see if it works.  
Note that in the Fallback example to add pipeline we are using the `IHttpClientBuilder.WithResiliencePipeline<TContext>` extension method with a string context (fallbackAnswer).


## 😹 Nuances of the use of samples

Service, that uses `HttpClient` (`AskCatService`) is placed in the 'Shared' project. It handles the `HttpPolicyResultException` exception, if it occurs, in the `GetCatFactAsync` method.
To mimic transient errors, uri is randomized and the `HandlerThatMakesTransientErrorFrom404` handler is used to randomly generate one of the transient errors.  
To beautify the console output, the 'Spectre.Console', 'vertical-spectreconsolelogger' nuget packages is used.  
To simplify exception output, minimal console options provided by the `vertical-spectreconsolelogger' package are used.  
The joke samples use cat-friendly service https://catfact.ninja/ . The cat in the photo is real, but the photo has been processed by AI.  
