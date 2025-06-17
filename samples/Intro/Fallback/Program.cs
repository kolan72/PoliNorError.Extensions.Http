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
				.WithResiliencePipeline((emptyBuilder) =>
				{
					return emptyBuilder
							.AddPolicyHandler((string context, IServiceProvider __) =>
											CatPolicies.GetOuterFallbackPolicy(loggerTest, context))
							.AddPolicyHandler((IServiceProvider sp) =>
							{
								var innerLogger = sp.GetRequiredService<ILogger>();
								return CatPolicies.GetFinalHandlerRetryPolicy(innerLogger);
							})
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				}
				, fallbackAnswer)
				//This handler is used here to mimic service resiliency problems.
				.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			UtilsConsole.PrintHello();

			Thread.Sleep(1000);

			using (var provider = services.BuildServiceProvider())
			{
				var service = provider.GetRequiredService<IAskCatService>();
				await CatFactManager.GetCatFactOnFallback(service, loggerTest);
			}

			UtilsConsole.PrintBye();
		}
	}
}

