namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Interface for building <see cref="Pipeline"/> by using the overall context.
	/// </summary>
	/// <typeparam name="TContext"></typeparam>
	public interface IPipelineBuilder<in TContext> : IWithPolicyHandlerExceptionFilter<IPipelineBuilder<TContext>>
	{
		/// <summary>
		///  Builds a <see cref="Pipeline"/>.
		/// </summary>
		/// <param name="context">Overall context.</param>
		/// <returns>Pipeline</returns>
		Pipeline Build(TContext context);
	}
}
