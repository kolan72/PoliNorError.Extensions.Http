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

			//Uncomment this line to use IHttpClientFactory.
			//services.AddNamedCatClientWithPipeline(loggerTest)
			//		.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			//Uncomment this line to work with the explicitly created pipeline
			//services.AddCatClientWithExplicitlyCreatedPipeline(loggerTest)
			//		.AddHttpMessageHandler<HandlerThatMakesTransientErrorFrom404>();

			UtilsConsole.PrintHello();

			Thread.Sleep(1000);

			await using (var provider = services.BuildServiceProvider())
			{
				var service = provider.GetRequiredService<IAskCatService>();
				await CatFactManager.GetCatFactOnRetry(service, loggerTest);
			}

			UtilsConsole.PrintBye();
		}
	}
}
