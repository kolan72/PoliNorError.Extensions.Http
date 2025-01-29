using Microsoft.Extensions.DependencyInjection;
using System;

namespace PoliNorError.Extensions.Http
{
	public static class HttpClientBuilderExtensions
	{
		/// <summary>
		/// Adds <see cref="Pipeline"/> pipeline to <see cref="IHttpClientBuilder"/> by using  <paramref name="pipelineFactory"/>.
		/// </summary>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipelineFactory">Factory to create pipeline.</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline(this IHttpClientBuilder builder, Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory)
		{
			var emptyConfiguration = PipelineBuilder.Create();
			var completedConfiguration = pipelineFactory(emptyConfiguration);
			builder.ApplyPipeline(completedConfiguration.Build());
			return builder;
		}

		/// <summary>
		/// Adds <see cref="Pipeline"/> pipeline to <see cref="IHttpClientBuilder"/> by using  <paramref name="pipelineFactory"/> with overall context.
		/// </summary>
		/// <typeparam name="TContext">Overall context type.</typeparam>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipelineFactory">Factory to create pipeline.</param>
		/// <param name="context">Overall context.</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline<TContext>(this IHttpClientBuilder builder, Func<IEmptyPipelineBuilder<TContext>, IPipelineBuilder<TContext>> pipelineFactory, TContext context)
		{
			var emptyConfiguration = PipelineBuilder<TContext>.Create();
			var completedConfiguration = pipelineFactory(emptyConfiguration);
			builder.ApplyPipeline(completedConfiguration.Build(context));
			return builder;
		}

		/// <summary>
		/// Adds created <see cref="Pipeline"/> pipeline to <see cref="IHttpClientBuilder"/>.
		/// </summary>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipeline">Pipeline</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline(this IHttpClientBuilder builder, Pipeline pipeline)
		{
			builder.ApplyPipeline(pipeline);
			return builder;
		}

		private static void ApplyPipeline(this IHttpClientBuilder builder, Pipeline pipeline)
		{
			foreach (var f in pipeline.HandlerChain)
			{
				builder.AddHttpMessageHandler(f);
			}
		}
	}
}
