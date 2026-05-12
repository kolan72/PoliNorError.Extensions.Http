using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using static PoliNorError.Extensions.Http.IncompletePipelineBuilder;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents a builder for a <see cref="Pipeline"/>.
	/// </summary>
	public class PipelineBuilder : IPipelineBuilder
	{
		private readonly IEnumerable<Func<IServiceProvider, IPolicyBase>> _factories;
		private readonly IPipelinePolicyItem _lastPipelinePolicyItem;
		private readonly HttpErrorFilterCriteria _errorsToHandle;
		private readonly ILoggerFactory _loggerFactory;

		internal PipelineBuilder(
			IEnumerable<Func<IServiceProvider, IPolicyBase>> factories, 
			IPipelinePolicyItem lastPipelinePolicyItem, 
			HttpErrorFilterCriteria errorsToHandle,
			ILoggerFactory loggerFactory = null)
		{
			_factories = factories;
			_lastPipelinePolicyItem = lastPipelinePolicyItem;
			_errorsToHandle = errorsToHandle;
			_loggerFactory = loggerFactory;
		}

		/// <summary>
		/// Creates a <see cref="EmptyPipelineBuilder"/>.
		/// </summary>
		/// <returns><see cref="EmptyPipelineBuilder"/></returns>
		public static EmptyPipelineBuilder Create()
		{
			return new EmptyPipelineBuilder(new IncompletePipelineBuilder());
		}

		/// <summary>
		/// Creates a <see cref="EmptyPipelineBuilder"/> with logging enabled.
		/// </summary>
		/// <param name="loggerFactory">The logger factory for creating loggers.</param>
		/// <returns><see cref="EmptyPipelineBuilder"/></returns>
		public static EmptyPipelineBuilder Create(ILoggerFactory loggerFactory)
		{
			return new EmptyPipelineBuilder(new IncompletePipelineBuilder(loggerFactory));
		}

		public Pipeline Build()
		{
			var allPolicies = _factories.ToArray();
			var handlers = new List<Func<IServiceProvider, DelegatingHandler>>();
			ILogger<PolicyHttpMessageHandler> logger = null;
			if (_loggerFactory != null)
			{
				logger = _loggerFactory.CreateLogger<PolicyHttpMessageHandler>();
			}

			for (var i = 0; i < allPolicies.Length - 1; i++)
			{
				var policyFunc = allPolicies[i];
				handlers.Add((sp) => 
				{ 
					var policy = policyFunc(sp); 
					return PolicyHttpMessageHandler.CreateOuterHandler(policy, logger);
				});
			}

			handlers.Add((sp) =>
			{
				var policyFunc = allPolicies[allPolicies.Length - 1];
				var policy = policyFunc(sp);
				return PolicyHttpMessageHandler.CreateFinalHandler(policy, _errorsToHandle, logger);
			});

			return new Pipeline(handlers);
		}

		public IPipelineBuilder IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception
		{
			_lastPipelinePolicyItem.IncludeExceptionForFinalHandler<TException>();
			return this;
		}
	}
}
