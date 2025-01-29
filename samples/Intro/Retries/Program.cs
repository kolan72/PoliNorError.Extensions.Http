using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared;
using PoliNorError.Extensions.Http;
using System.Threading;

namespace Retries
{
	internal static class Program
	{
		private static async Task Main(string[] args)
		{
			var services = new ServiceCollection();

			var loggerTest = LogFactory.CreateLogger();
			services.AddSingleton(loggerTest);

			services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

			_ = services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) =>
				{
					return emptyBuilder
							.AddPolicyHandler(CatPolicies
												.GetOuterRetryPolicy(loggerTest))
							.AddPolicyHandler((IServiceProvider sp) =>
							{
								var innerLogger = sp.GetRequiredService<ILogger>();
								return CatPolicies.GetFinalHandlerRetryPolicy(innerLogger);
							})
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				})
				//This handler is used here to mimic service resiliency problems.
				.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			//Uncomment this line to work with the named client
			//services.AddNamedCatClientWithPipeline(loggerTest)
			//		.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			//Uncomment this line to work with the explicitly created pipeline
			//services.AddCatClientWithExplicitlyCreatedPipeline(loggerTest)
			//		.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();


			var provider = services.BuildServiceProvider();
			var service = provider.GetRequiredService<IAskCatService>();

			UtilsConsole.PrintHello();

			Thread.Sleep(1000);

			await AskCatInCyclic(service, loggerTest);

			UtilsConsole.PrintBye();
		}

		private static async Task AskCatInCyclic(IAskCatService retryAskingCatService, ILogger loggerTest)
		{
			var shouldContinue = true;
			using var cts = new CancellationTokenSource();

			var seanceNumber = 0;

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
					if (seanceNumber == 0)
						Console.WriteLine("You are lucky to get a quick answer, my cat in high spirits today.");

					loggerTest.LogInformation("The cat's answer is correct: {Answer}", answer.Answer);

					shouldContinue = UtilsConsole.PrintContinueToAskPrompt();
					if (shouldContinue)
						seanceNumber++;
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
								loggerTest.LogError("Failed status code: {StatusCode}.", httpException.FailedResponseData.StatusCode);
							}

							if (!UtilsConsole.PrintContinueToAskPrompt())
							{
								cts.Cancel();
							}
							else
							{
								seanceNumber++;
							}
						}
						else
						{
							loggerTest.LogError(answer.Error, "Non-transient service exception occurs.");
							shouldContinue = false;
						}
					}
				}
			} while (shouldContinue);
		}
	}
}
