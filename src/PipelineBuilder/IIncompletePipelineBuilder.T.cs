namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents a pipeline builder with overall context that is incomplete. This interface is primarily for internal use.
	/// </summary>
	/// <typeparam name="TContext">Overall context type.</typeparam>
	public interface IIncompletePipelineBuilder<TContext> : IPolicyHandlerStorage<IIncompletePipelineBuilder<TContext>, TContext>,
															IWithPolicyHandlerExceptionFilter<IIncompletePipelineBuilder<TContext>>
	{
		IPipelineBuilder<TContext> AsFinalHandler(HttpErrorFilterCriteria errorsToHandle);
	}
}
