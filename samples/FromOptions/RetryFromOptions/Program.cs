using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.Http;
using Shared;

namespace RetryFromOptions
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
							.AddRetryHandler(3, GetFinalHandlerRetryPolicyOptions(loggerTest))
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				})
				//This handler is used here to mimic service resiliency problems.
				.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			UtilsConsole.PrintHello();

			Thread.Sleep(1000);

			using (var provider = services.BuildServiceProvider())
			{
				var service = provider.GetRequiredService<IAskCatService>();
				await CatFactManager.GetCatFactOnRetry(service, loggerTest);
			}

			UtilsConsole.PrintBye();
		}

		private static RetryPolicyOptions GetFinalHandlerRetryPolicyOptions(ILogger logger)
		{
			const string policyName = "FinalHandlerAskCatRetryPolicy";
			return new RetryPolicyOptions()
			{
				PolicyName = policyName,

				ConfigureErrorProcessing = (bp) =>
					bp.WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
					{
						logger.LogError(ex,
										"Policy {PolicyName} handled an exception on attempt {Attempt}:",
										policyName,
										pi.GetRetryCount() + 1);
						if (ex is FailedHttpResponseException failedException)
						{
							logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}", failedException.FailedResponseData.StatusCode);
						}
					})
					.WithDelayBetweenRetries((_, __) => TimeSpan.FromMilliseconds(1000)),

				ConfigurePolicyResultHandling = (handlers) => handlers.AddHandler
				(
					(pr, _) =>
					{
						if (pr.IsPolicySuccess)
							logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
						else if (pr.IsFailed)
						{
							logger.LogWarning("{Errors} exceptions were thrown during handling by {PolicyName}.",
												pr.Errors.Count(),
												pr.PolicyName);
							if (pr.UnprocessedError is not null)
							{
								logger.LogError(pr.UnprocessedError,
												"UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
												policyName);
							}
						}
					}
				)
			};
		}
	}
}
