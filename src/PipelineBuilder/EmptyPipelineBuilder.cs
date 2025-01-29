using System;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This interface represents an empty PipelineBuilder, primarily for internal use.
	/// </summary>
	public interface IEmptyPipelineBuilder : IPolicyHandlerStorage<IIncompletePipelineBuilder>
	{
	}

	/// <summary>
	/// Represents an empty pipeline builder, primarily for internal use.
	/// </summary>
	public class EmptyPipelineBuilder : IEmptyPipelineBuilder
	{
		private readonly IIncompletePipelineBuilder _pipelineConfiguration;
		internal EmptyPipelineBuilder(IIncompletePipelineBuilder pipelineConfiguration)
		{
			_pipelineConfiguration = pipelineConfiguration;
		}

		public IIncompletePipelineBuilder AddPolicyHandler<T>(T policy) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			return _pipelineConfiguration.AddPolicyHandler(policy);
		}

		public IIncompletePipelineBuilder AddPolicyHandler<T>(Func<IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase
		{
			return _pipelineConfiguration.AddPolicyHandler(policyFactory);
		}
	}
}
