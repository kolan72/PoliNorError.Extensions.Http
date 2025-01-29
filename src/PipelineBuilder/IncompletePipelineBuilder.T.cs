using System;
using System.Collections.Generic;
using System.Linq;

namespace PoliNorError.Extensions.Http
{
	public class IncompletePipelineBuilder<TContext> : IIncompletePipelineBuilder<TContext>
	{
		private readonly List<IPipelinePolicyItem> _pipelinePolicyItems = new List<IPipelinePolicyItem>();
		private IPipelinePolicyItem _curPipelinePolicyItem;

		public IIncompletePipelineBuilder<TContext> AddPolicyHandler<T>(T policy) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			CorrectPipelinePolicyItemFilter();
			_curPipelinePolicyItem = new PipelinePolicyItem<T>((_, __) => policy);
			_pipelinePolicyItems.Add(_curPipelinePolicyItem);
			return this;
		}

		public IIncompletePipelineBuilder<TContext> AddPolicyHandler<T>(Func<IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			CorrectPipelinePolicyItemFilter();
			_curPipelinePolicyItem = new PipelinePolicyItem<T>((_, sp) => policyFactory(sp));
			_pipelinePolicyItems.Add(_curPipelinePolicyItem);
			return this;
		}

		public IIncompletePipelineBuilder<TContext> AddPolicyHandler<T>(Func<TContext, IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			CorrectPipelinePolicyItemFilter();
			_curPipelinePolicyItem = new PipelinePolicyItem<T>(policyFactory);
			_pipelinePolicyItems.Add(_curPipelinePolicyItem);
			return this;
		}

		public IPipelineBuilder<TContext> AsFinalHandler(IHttpErrorFilter errorsToHandle)
		{
			_curPipelinePolicyItem.AsFinalHandler(errorsToHandle);
			return new PipelineBuilder<TContext>(_pipelinePolicyItems.Select(pit => pit.PolicyFactory), _curPipelinePolicyItem, errorsToHandle);
		}

		public IIncompletePipelineBuilder<TContext> IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception
		{
			_curPipelinePolicyItem.IncludeException<TException>();
			return this;
		}

		private void CorrectPipelinePolicyItemFilter()
		{
#pragma warning disable RCS1146 // Use conditional access.
			if (_curPipelinePolicyItem != null)
			{
				_curPipelinePolicyItem.CorrectFiler();
			}
#pragma warning restore RCS1146 // Use conditional access.
		}

		private sealed class PipelinePolicyItem<T> : IPipelinePolicyItem where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			private Func<TContext, IServiceProvider, T> _policyFunc;
			private bool _hasIncludeError;

			public PipelinePolicyItem(Func<TContext, IServiceProvider, T> policyFunc)
			{
				_policyFunc = policyFunc;
			}

			public Func<TContext, IServiceProvider, IPolicyBase> PolicyFactory => (ctx, sp) => _policyFunc(ctx, sp);

			public void AsFinalHandler(IHttpErrorFilter errorsToHandle)
			{
				T p(T pEx)
				{
					return pEx.WithErrorsFilter(errorsToHandle);
				}
				var prevF = _policyFunc;
				_policyFunc = (ctx, sp) => p(prevF(ctx, sp));
			}

			public void CorrectFiler()
			{
				if (_hasIncludeError) return;
				var prevF = _policyFunc;
				_policyFunc = (ctx, sp) =>
				{
					var policy = prevF(ctx, sp);
					return policy.IncludeError<HttpPolicyResultException>();
				};
			}

			public void IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception
			{
				var prevF = _policyFunc;
				_policyFunc = (ctx, sp) =>
				{
					var policy = prevF(ctx, sp);
					return policy.IncludeInnerError(func);
				};
				_hasIncludeError = true;
			}

			public void IncludeExceptionForFinalHandler<TException>(Func<TException, bool> func = null) where TException : Exception
			{
				var prevF = _policyFunc;
				_policyFunc = (ctx, sp) =>
				{
					var policy = prevF(ctx, sp);
					return policy.IncludeError(func);
				};
			}
		}

		internal interface IPipelinePolicyItem : IPipelinePolicyItemBase
		{
			Func<TContext, IServiceProvider, IPolicyBase> PolicyFactory { get; }
		}
	}
}
