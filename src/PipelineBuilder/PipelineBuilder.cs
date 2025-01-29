using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
		private readonly IHttpErrorFilter _errorsToHandle;

		internal PipelineBuilder(IEnumerable<Func<IServiceProvider, IPolicyBase>> factories, IPipelinePolicyItem lastPipelinePolicyItem, IHttpErrorFilter errorsToHandle)
		{
			_factories = factories;
			_lastPipelinePolicyItem = lastPipelinePolicyItem;
			_errorsToHandle = errorsToHandle;
		}

		/// <summary>
		/// Creates a <see cref="EmptyPipelineBuilder"/>.
		/// </summary>
		/// <returns><see cref="EmptyPipelineBuilder"/></returns>
		public static EmptyPipelineBuilder Create()
		{
			return new EmptyPipelineBuilder(new IncompletePipelineBuilder());
		}

		public Pipeline Build()
		{
			var allPolicies = _factories.ToArray();
			var handlers = new List<Func<IServiceProvider, DelegatingHandler>>();
			for (var i = 0; i < allPolicies.Length - 1; i++)
			{
				var policyFunc = allPolicies[i];
				handlers.Add((sp) => { var policy = policyFunc(sp); return PolicyHttpMessageHandler.CreateOuterHandler(policy); });
			}
			handlers.Add((sp) =>
			{
				var policyFunc = allPolicies[allPolicies.Length - 1];
				var policy = policyFunc(sp);
				return PolicyHttpMessageHandler.CreateFinalHandler(policy, _errorsToHandle);
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
