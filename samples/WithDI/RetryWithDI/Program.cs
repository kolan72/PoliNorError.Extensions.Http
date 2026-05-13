using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.DependencyInjection;
using PoliNorError.Extensions.Http;
using Shared;
using Shared.Policies;
using System.Reflection;

namespace RetryWithDI
{
	internal static class Program
	{
		private static async Task Main(string[] args)
		{
			var services = new ServiceCollection();

			var loggerTest = LogFactory.CreateLogger();
			services.AddSingleton(loggerTest);

			// Register policy builders via DI
			services.AddPoliNorError(
				typeof(CatPolicies).Assembly);      // Shared assembly

			services.AddTransient<HandlerThatMakesTransientErrorFrom404>();

			_ = services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) =>
				{
					return emptyBuilder
							// Use policy builders from DI
							.AddPolicyHandler((IServiceProvider sp) =>
							{
								var builder = sp.GetRequiredService<IPolicyBuilder<OuterRetryPolicyBuilder>>();
								return (RetryPolicy)builder.Build();
							})
							.AddPolicyHandler((IServiceProvider sp) =>
							{
								var builder = sp.GetRequiredService<IPolicyBuilder<FinalRetryPolicyBuilder>>();
								return (RetryPolicy)builder.Build();
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
	}
}
