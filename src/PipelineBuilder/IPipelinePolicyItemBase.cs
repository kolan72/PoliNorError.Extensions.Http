using System;

namespace PoliNorError.Extensions.Http
{
	internal interface IPipelinePolicyItemBase
	{
		void IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception;
		void IncludeExceptionForFinalHandler<TException>(Func<TException, bool> func = null) where TException : Exception;
		void CorrectFiler();
		void AsFinalHandler(IHttpErrorFilter errorsToHandle);
	}
}
