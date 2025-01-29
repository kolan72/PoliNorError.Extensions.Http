using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError.Extensions.Http;

using Shared;

namespace Fallback
{
	internal static class ServiceCollectionExtensions
	{
		public static IHttpClientBuilder AddCatClientWithPipelineOfHandlersCreatedInDifferentWays(this IServiceCollection services, ILogger logger, string fallbackAnswer)
		{
			return services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) => {
					return emptyBuilder
							.AddPolicyHandler((string context, IServiceProvider sp) => 
											CatPolicies.GetOuterFallbackPolicy(logger, context))
							.AddPolicyHandler((IServiceProvider sp) => {
								var innerLogger = sp.GetRequiredService<ILogger>();
								return CatPolicies.GetFinalHandlerRetryPolicy(innerLogger);
							})
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				}
				, fallbackAnswer);
		}


		public static IHttpClientBuilder AddCatClientWithPipelineOfHandlersCreatedInDifferentWays(this IServiceCollection services, ILogger logger)
		{
			return services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) => {
					return emptyBuilder
							.AddPolicyHandler(CatPolicies.GetOuterFallbackPolicy(logger))
							.AddPolicyHandler((IServiceProvider sp) => {
								var innerLogger = sp.GetRequiredService<ILogger>();
								return CatPolicies.GetFinalHandlerRetryPolicy(innerLogger);
							})
							.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors());
				}
														);
		}

		public static IHttpClientBuilder AddCatClientWithPipelineOfHandlersCreatedByPolicies(this IServiceCollection services, ILogger logger)
		{
			return services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) => emptyBuilder
														.AddPolicyHandler(CatPolicies.GetOuterFallbackPolicy(logger))
														.AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(logger))
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
		}
	}
}
