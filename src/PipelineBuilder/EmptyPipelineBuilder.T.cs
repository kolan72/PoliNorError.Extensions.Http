using System;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This interface represents an empty PipelineBuilder, primarily for internal use.
	/// </summary>
	public interface IEmptyPipelineBuilder<TContext> : IPolicyHandlerStorage<IIncompletePipelineBuilder<TContext>, TContext>
	{}

	/// <summary>
	/// Represents an empty pipeline builder with overall context, primarily for internal use.
	/// </summary>
	public class EmptyPipelineBuilder<TContext> : IEmptyPipelineBuilder<TContext>
	{
		private readonly IIncompletePipelineBuilder<TContext> _incompletePipelineBuilder;
		public EmptyPipelineBuilder(IIncompletePipelineBuilder<TContext>  incompletePipelineBuilder)
		{
			_incompletePipelineBuilder = incompletePipelineBuilder;
		}

		public IIncompletePipelineBuilder<TContext> AddPolicyHandler<T>(Func<TContext, IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			return _incompletePipelineBuilder.AddPolicyHandler(policyFactory);
		}

		public IIncompletePipelineBuilder<TContext> AddPolicyHandler<T>(T policy) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			return _incompletePipelineBuilder.AddPolicyHandler(policy);
		}

		public IIncompletePipelineBuilder<TContext> AddPolicyHandler<T>(Func<IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			return _incompletePipelineBuilder.AddPolicyHandler(policyFactory);
		}
	}
}
