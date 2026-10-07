using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoliNorError;
using PoliNorError.Extensions.Http;
using Shared;
using System.IO;

namespace RetryWithOptions
{
	internal static class Program
	{
		private static async Task Main(string[] args)
		{
			var services = new ServiceCollection();

			var loggerTest = LogFactory.CreateLogger();
			services.AddSingleton(loggerTest);

			// Configure RetryOptions from appSettings.json
			IConfiguration configuration = new ConfigurationBuilder()
							 .SetBasePath(Directory.GetCurrentDirectory())
							 .AddJsonFile("appSettings.json", false)
							 .Build();

			services.Configure<RetryOptions>(configuration.GetSection("RetryOptions"));

			services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

			_ = services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) =>
				{
					return emptyBuilder
							.AddPolicyHandler(CatPolicies
												.GetOuterRetryPolicy(loggerTest))
							.AddRetryHandler(
								(IServiceProvider sp) =>
								{
									var retryOptions = sp.GetRequiredService<IOptions<RetryOptions>>().Value;

									var logger = sp.GetRequiredService<ILogger>();
									return GetRetryPolicyOptions(retryOptions, logger);
								})
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				})
				//This handler is used here to mimic service resiliency problems.
				.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			UtilsConsole.PrintHello();

			Thread.Sleep(1000);

			await using (var provider = services.BuildServiceProvider())
			{
				var service = provider.GetRequiredService<IAskCatService>();
				await CatFactManager.GetCatFactOnRetry(service, loggerTest);
			}

			UtilsConsole.PrintBye();
		}

		private static RetryPolicy GetRetryPolicyOptions(RetryOptions retryOptions, ILogger logger)
		{
			return new RetryPolicy(3)
						.WithPolicyName(retryOptions.PolicyName)
						.WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
						{
							logger.LogError(ex,
												"Policy {PolicyName} handled an exception on attempt {Attempt}:",
												retryOptions.PolicyName,
												pi.GetRetryCount() + 1);
							if (ex is FailedHttpResponseException failedException)
							{
								logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}", failedException.FailedResponseData.StatusCode);
							}
						})
						.AddPolicyResultHandler<HttpResponseMessage>(pr =>
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
													retryOptions.PolicyName);
								}
							}
						})
						.WithWait(TimeSpan.FromMilliseconds(retryOptions.DelayMilliseconds));
		}
	}
}
