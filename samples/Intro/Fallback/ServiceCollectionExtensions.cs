using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError.Extensions.Http;

using Shared;

namespace Fallback
{
	internal static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Extension method that provides shorthand for configuring the <see cref="ServiceCollection"/> in <see cref="Program.Main(string[])"/>
		/// </summary>
		/// <param name="services"><see cref="IServiceCollection"/></param>
		/// <param name="logger"><see cref="ILogger"/></param>
		/// <param name="pipelineContext">Overall context of the pipeline.</param>
		/// <returns></returns>
		public static IHttpClientBuilder AddCatClientWithPipelineOfHandlersCreatedInDifferentWays(this IServiceCollection services, ILogger logger, string pipelineContext)
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
				, pipelineContext);
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
