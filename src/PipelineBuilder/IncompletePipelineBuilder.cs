using System;
using System.Collections.Generic;
using System.Linq;

namespace PoliNorError.Extensions.Http
{
	public class IncompletePipelineBuilder : IIncompletePipelineBuilder
	{
		private readonly List<IPipelinePolicyItem> _pipelinePolicyItems = new List<IPipelinePolicyItem>();
		private IPipelinePolicyItem _curPipelinePolicyItem;

		public IIncompletePipelineBuilder AddPolicyHandler<T>(T policy) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			CorrectPipelinePolicyItemFilter();
			_curPipelinePolicyItem = new PipelinePolicyItem<T>((_) => policy);
			_pipelinePolicyItems.Add(_curPipelinePolicyItem);
			return this;
		}

		public IIncompletePipelineBuilder AddPolicyHandler<T>(Func<IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			CorrectPipelinePolicyItemFilter();
			_curPipelinePolicyItem = new PipelinePolicyItem<T>(policyFactory);
			_pipelinePolicyItems.Add(_curPipelinePolicyItem);
			return this;
		}

		public IIncompletePipelineBuilder IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception
		{
			_curPipelinePolicyItem.IncludeException<TException>();
			return this;
		}

		public IPipelineBuilder AsFinalHandler(IHttpErrorFilter errorsToHandle)
		{
			_curPipelinePolicyItem.AsFinalHandler(errorsToHandle);
			return new PipelineBuilder(_pipelinePolicyItems.Select(pit => pit.PolicyFactory), _curPipelinePolicyItem, errorsToHandle);
		}

		private void CorrectPipelinePolicyItemFilter()
		{
#pragma warning disable RCS1146 // Use conditional access.
			// ReSharper disable once UseNullPropagation
			if (_curPipelinePolicyItem != null)
			{
				_curPipelinePolicyItem.CorrectFiler();
			}
#pragma warning restore RCS1146 // Use conditional access.
		}

		internal interface IPipelinePolicyItem : IPipelinePolicyItemBase
		{
			Func<IServiceProvider, IPolicyBase> PolicyFactory { get; }
		}

		private sealed class PipelinePolicyItem<T> : IPipelinePolicyItem where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			private Func<IServiceProvider, T> _policyFunc;
			private bool _hasIncludeError;

			public PipelinePolicyItem(Func<IServiceProvider, T> policyFunc)
			{
				_policyFunc = policyFunc;
			}

			public Func<IServiceProvider, IPolicyBase> PolicyFactory => (sp) => _policyFunc(sp);

			public void AsFinalHandler(IHttpErrorFilter errorsToHandle)
			{
				T p(T pEx)
				{
					return pEx.WithErrorsFilter(errorsToHandle);
				}
				var prevF = _policyFunc;
				_policyFunc = (sp) => p(prevF(sp));
			}

			public void CorrectFiler()
			{
				if (_hasIncludeError) return;
				var prevF = _policyFunc;
				_policyFunc = (sp) =>
				{
					var policy = prevF(sp);
					return policy.IncludeError<HttpPolicyResultException>();
				};
			}

			public void IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception
			{
				var prevF = _policyFunc;
				_policyFunc = (sp) =>
				{
					var policy = prevF(sp);
					return policy.IncludeInnerError(func);
				};
				_hasIncludeError = true;
			}

			public void IncludeExceptionForFinalHandler<TException>(Func<TException, bool> func = null) where TException : Exception
			{
				var prevF = _policyFunc;
				_policyFunc = (sp) =>
				{
					var policy = prevF(sp);
					return policy.IncludeError(func);
				};
			}
		}
	}
}
