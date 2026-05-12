using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace PoliNorError.Extensions.Http
{
	public static class HttpClientBuilderExtensions
	{
		/// <summary>
		/// Adds <see cref="Pipeline"/> pipeline to <see cref="IHttpClientBuilder"/> by using <paramref name="pipelineFactory"/>.
		/// </summary>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipelineFactory">Factory to create pipeline.</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline(this IHttpClientBuilder builder, Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory)
		{
			ThrowHelper.ThrowIfNull(builder);
			ThrowHelper.ThrowIfNull(pipelineFactory);

			var emptyConfiguration = PipelineBuilder.Create();
			var completedConfiguration = pipelineFactory(emptyConfiguration);
			builder.ApplyPipeline(completedConfiguration.Build());
			return builder;
		}

		/// <summary>
		/// Adds <see cref="Pipeline"/> pipeline with structured logging and telemetry to <see cref="IHttpClientBuilder"/> by using <paramref name="pipelineFactory"/>.
		/// </summary>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipelineFactory">Factory to create pipeline.</param>
		/// <param name="loggerFactory">Logger factory for creating loggers. Pass null to disable logging.</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline(
			this IHttpClientBuilder builder, 
			Func<IEmptyPipelineBuilder, IPipelineBuilder> pipelineFactory,
			ILoggerFactory loggerFactory)
		{
			ThrowHelper.ThrowIfNull(builder);
			ThrowHelper.ThrowIfNull(pipelineFactory);

			var emptyConfiguration = PipelineBuilder.Create(loggerFactory);
			var completedConfiguration = pipelineFactory(emptyConfiguration);
			builder.ApplyPipeline(completedConfiguration.Build());
			return builder;
		}

		/// <summary>
		/// Adds <see cref="Pipeline"/> pipeline to <see cref="IHttpClientBuilder"/> by using <paramref name="pipelineFactory"/> with overall context.
		/// </summary>
		/// <typeparam name="TContext">Overall context type.</typeparam>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipelineFactory">Factory to create pipeline.</param>
		/// <param name="context">Overall context.</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline<TContext>(
			this IHttpClientBuilder builder, 
			Func<IEmptyPipelineBuilder<TContext>, IPipelineBuilder<TContext>> pipelineFactory, 
			TContext context)
		{
			ThrowHelper.ThrowIfNull(builder);
			ThrowHelper.ThrowIfNull(pipelineFactory);

			var emptyConfiguration = PipelineBuilder<TContext>.Create();
			var completedConfiguration = pipelineFactory(emptyConfiguration);
			builder.ApplyPipeline(completedConfiguration.Build(context));
			return builder;
		}

		/// <summary>
		/// Adds created <see cref="Pipeline"/> pipeline to <see cref="IHttpClientBuilder"/>. Use the <see cref="PipelineBuilder"/> class to create a <see cref="Pipeline"/> class.
		/// </summary>
		/// <param name="builder"><see cref="IHttpClientBuilder"/></param>
		/// <param name="pipeline">Pipeline</param>
		/// <returns></returns>
		public static IHttpClientBuilder WithResiliencePipeline(this IHttpClientBuilder builder, Pipeline pipeline)
		{
			ThrowHelper.ThrowIfNull(builder);
			ThrowHelper.ThrowIfNull(pipeline);

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
