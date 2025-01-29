using System.Net.Http;
namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents a pipeline builder that is incomplete. This interface is primarily for internal use.
	/// </summary>
	public interface IIncompletePipelineBuilder : IPolicyHandlerStorage<IIncompletePipelineBuilder>,
													IWithPolicyHandlerExceptionFilter<IIncompletePipelineBuilder>
	{
		/// <summary>
		/// Finishes building the <see cref="Pipeline"/> by adding <see cref="IHttpErrorFilter"/> to the last <see cref="DelegatingHandler"/> in the <see cref="IPipelineBuilder"/> collection.
		/// </summary>
		/// <param name="errorsToHandle">Filter for http errors.</param>
		/// <returns></returns>
		IPipelineBuilder AsFinalHandler(IHttpErrorFilter errorsToHandle);
	}
}
