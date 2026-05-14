using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError.Extensions.Http;
using Shared;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace FallbackFromOptions
{
	internal static class Program
	{
		private static async Task Main(string[] _)
		{
			var services = new ServiceCollection();

			var loggerTest = LogFactory.CreateLogger();
			services.AddSingleton(loggerTest);

			services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

			const string fallbackAnswer = "Meow!!!";
			services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) =>
				{
					return emptyBuilder
							.AddFallbackHandler(
								(_) => Task.FromResult(GetCustomFallbackCatAnswer(fallbackAnswer)),
								GetOuterFallbackPolicyOptions(loggerTest))
							.AddPolicyHandler((IServiceProvider sp) =>
							{
								var innerLogger = sp.GetRequiredService<ILogger>();
								return CatPolicies.GetFinalHandlerRetryPolicy(innerLogger);
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
				await CatFactManager.GetCatFactOnFallback(service, loggerTest);
			}

			UtilsConsole.PrintBye();
		}

		private static FallbackPolicyOptions GetOuterFallbackPolicyOptions(ILogger logger)
		{
			return new FallbackPolicyOptions()
			{
				PolicyName = "CatAnswerFallbackPolicy",
				ConfigurePolicyResultHandling = (handlers) => handlers.AddHandler((pr, _) =>
				{
					if (pr.IsPolicySuccess)
						logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
				})
			};
		}

		private static HttpResponseMessage GetCustomFallbackCatAnswer(string customAnswer)
			=> new HttpResponseMessage()
			{
				Content = new StringContent(
					JsonSerializer.Serialize(new CatResponse() { Fact = customAnswer }),
					Encoding.UTF8,
					"application/json")
			};
	}
}
