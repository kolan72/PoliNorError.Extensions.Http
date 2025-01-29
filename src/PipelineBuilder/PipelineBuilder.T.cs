using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents a builder for a <see cref="Pipeline"/> with overall context.
	/// </summary>
	/// <typeparam name="TContext">Overall context type</typeparam>
	public class PipelineBuilder<TContext> : IPipelineBuilder<TContext>
	{
		private readonly IEnumerable<Func<TContext, IServiceProvider, IPolicyBase>> _factories;
		private readonly IncompletePipelineBuilder<TContext>.IPipelinePolicyItem _lastPipelinePolicyItem;
		private readonly IHttpErrorFilter _errorsToHandle;

		internal PipelineBuilder(IEnumerable<Func<TContext, IServiceProvider, IPolicyBase>> factories, IncompletePipelineBuilder<TContext>.IPipelinePolicyItem lastPipelinePolicyItem, IHttpErrorFilter errorsToHandle)
		{
			_factories = factories;
			_lastPipelinePolicyItem = lastPipelinePolicyItem;
			_errorsToHandle = errorsToHandle;
		}

		/// <summary>
		/// Creates a <see cref="EmptyPipelineBuilder{TContext}"/>.
		/// </summary>
		/// <returns><see cref="EmptyPipelineBuilder{TContext}"/></returns>
		public static EmptyPipelineBuilder<TContext> Create()
		{
			return new EmptyPipelineBuilder<TContext>(new IncompletePipelineBuilder<TContext>());
		}

		public Pipeline Build(TContext context)
		{
			var allPolicies = _factories.ToArray();
			var handlers = new List<Func<IServiceProvider, DelegatingHandler>>();
			for (var i = 0; i < allPolicies.Length - 1; i++)
			{
				var policyFunc = allPolicies[i];
				handlers.Add((sp) => { var policy = policyFunc(context, sp); return PolicyHttpMessageHandler.CreateOuterHandler(policy); });
			}
			handlers.Add((sp) =>
			{
				var policyFunc = allPolicies[allPolicies.Length - 1];
				var policy = policyFunc(context, sp);
				return PolicyHttpMessageHandler.CreateFinalHandler(policy, _errorsToHandle);
			});
			return new Pipeline(handlers);
		}

		public IPipelineBuilder<TContext> IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception
		{
			_lastPipelinePolicyItem.IncludeExceptionForFinalHandler<TException>();
			return this;
		}
	}
}
