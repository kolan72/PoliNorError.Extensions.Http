namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Interface for building <see cref="Pipeline"/>.
	/// </summary>
	public interface IPipelineBuilder : IWithPolicyHandlerExceptionFilter<IPipelineBuilder>
	{
		/// <summary>
		/// Builds a <see cref="Pipeline"/>.
		/// </summary>
		/// <returns></returns>
		Pipeline Build();
	}
}
