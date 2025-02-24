using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliNorError.Extensions.Http;
using Shared;
using System;
using System.Net;

namespace Retries
{
	internal static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Extension method that provides shorthand for configuring the <see cref="ServiceCollection"/> in <see cref="Program.Main(string[])"/>
		/// </summary>
		/// <param name="services"><see cref="IServiceCollection"/></param>
		/// <param name="logger"><see cref="ILogger"/></param>
		/// <returns></returns>
		public static IHttpClientBuilder AddCatClientWithPipelineOfHandlersCreatedInDifferentWays(this IServiceCollection services, ILogger logger)
		{
			return services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) => {
															return emptyBuilder
																	.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(logger))
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
														.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(logger))
														.AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(logger))
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
		}

		public static IHttpClientBuilder AddNamedCatClientWithPipeline(this IServiceCollection services, ILogger logger)
		{
			return services
				.AddConfig()
				.AddTransient<IAskCatService, AskNamedCatService>()
				.AddNamedCatHttpClient()
				.WithResiliencePipeline((emptyBuilder) => emptyBuilder
														.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(logger))
														.AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(logger))
														.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors()));
		}

		public static IHttpClientBuilder AddCatClientWithExplicitlyCreatedPipeline(this IServiceCollection services, ILogger logger)
		{
			var pipelineInner = PipelineBuilder.Create()
								.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(logger))
								.AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(logger))
								.AsFinalHandler(HttpErrorFilter.HandleTransientHttpErrors())
								.Build();

			return services
				.AddConfig()
				.AddCatHttpClient()
				.WithResiliencePipeline(pipelineInner);
		}

		//Not use so far
		public static IHttpClientBuilder AddCatClientWithTwoPipelines(this IServiceCollection services, ILogger logger)
		{
			return services
			.AddConfig()
			.AddCatHttpClient()
			.WithResiliencePipeline((emptyBuilder) => emptyBuilder
													.AddPolicyHandler(CatPolicies.GetOuterRetryPolicy(logger))
													.AsFinalHandler(
																 HttpErrorFilter.None())
													)
			.WithResiliencePipeline((emptyBuilder) => emptyBuilder
													.AddPolicyHandler(CatPolicies.GetFinalHandlerRetryPolicy(logger))
													.AsFinalHandler(
																HttpErrorFilter.HandleTransientHttpErrors()
																//In this particular case, we add NotFound to the error filter
																//because we are mimicking service resilience problems.
																.OrStatusCode(HttpStatusCode.NotFound)));
		}

	}
}
