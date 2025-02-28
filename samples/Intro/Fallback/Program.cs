using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using PoliNorError.Extensions.Http;

namespace Fallback
{
	internal static class Program
	{
		static async Task Main(string[] _)
		{
			var services = new ServiceCollection();

			var loggerTest = LogFactory.CreateLogger();
			services.AddSingleton(loggerTest);

			services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

			const string fallbackAnswer = "Meow!!!";
			services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) => {
					return emptyBuilder
							.AddPolicyHandler((string context, IServiceProvider sp) =>
											CatPolicies.GetOuterFallbackPolicy(loggerTest, context))
							.AddPolicyHandler((IServiceProvider sp) => {
								var innerLogger = sp.GetRequiredService<ILogger>();
								return CatPolicies.GetFinalHandlerRetryPolicy(innerLogger);
							})
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				}
				, fallbackAnswer)
				//This handler is used here to mimic service resiliency problems.
				.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			var provider = services.BuildServiceProvider();
			var service = provider.GetRequiredService<IAskCatService>();

			UtilsConsole.PrintHello();

			Thread.Sleep(1000);

			await AskCat(service, loggerTest);

			UtilsConsole.PrintBye();
		}

		private static async Task AskCat(IAskCatService retryAskingCatService, ILogger loggerTest)
		{
			var shouldContinue = true;
			using var cts = new CancellationTokenSource();

			do 
			{
				if (cts.IsCancellationRequested)
				{
					UtilsConsole.PrintCancelAsking();
					break;
				}
				var answer = await retryAskingCatService.GetCatFactAsync(cts.Token);
				if (answer.IsOk)
				{
					loggerTest.LogInformation("The cat's answer is correct: {Answer}", answer.Answer);
					shouldContinue = UtilsConsole.PrintContinueToAskPrompt();
				}
				else
				{
					if (answer.IsCanceled == true)
					{
						UtilsConsole.PrintCancelAsking(true);
						shouldContinue = false;
					}
					else
					{
						UtilsConsole.PrintNoCorrectAnswer(answer.Error);
						if (answer.Error is HttpPolicyResultException httpException)
						{
							if (httpException.HasFailedResponse)
							{
								var statusCode = httpException.FailedResponseData.StatusCode;

								loggerTest.LogWarning("Inner exception status code: {StatusCode}.", statusCode);
							}

							if (!UtilsConsole.PrintContinueToAskPrompt())
							{
								cts.Cancel();
							}
						}
						else
						{
							loggerTest.LogError(answer.Error, "Non-transient service exception occurs.");
						}
					}
				}
			} while (shouldContinue);

		}
	}
}

