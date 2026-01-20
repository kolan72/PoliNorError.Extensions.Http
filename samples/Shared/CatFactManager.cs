using Microsoft.Extensions.Logging;
using PoliNorError.Extensions.Http;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
	public static class CatFactManager
	{
		public static async Task GetCatFactOnRetry(IAskCatService retryAskingCatService, ILogger loggerTest)
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
							loggerTest.LogError("Outer Policy {PolicyName} exceptions count {ErrorsCount}:"
								, httpException.PolicyResult.PolicyName
								, httpException.PolicyResult.Errors.Count());

							//  Check all exceptions that occurred during retries in the outer handler
							foreach (var error in httpException.PolicyResult.Errors)
							{
								var info = new { Type = error.GetType().Name, error.Message };
								loggerTest.LogError(error, "Outer policy exception {@ErrorInfo}", info);
							}

							// Identify which policy failed at the deepest level
							var rootResult = httpException.InnermostPolicyResult;
							loggerTest.LogError("Final Policy {PolicyName} exceptions count {ErrorsCount}:", rootResult.PolicyName, rootResult.Errors.Count());

							// Check all exceptions that occurred during retries in the innermost handler
							foreach (var error in rootResult.Errors)
							{
								var info = new {Type = error.GetType().Name, error.Message };
								loggerTest.LogError(error, "Final policy exception {@ErrorInfo}", info);
							}

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

		public static async Task GetCatFactOnFallback(IAskCatService retryAskingCatService, ILogger loggerTest)
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
