using System;

namespace PoliNorError.Extensions.Http
{
#pragma warning disable RCS1194 // Implement exception constructors.
	public class FailedHttpResponseException : Exception
#pragma warning restore RCS1194 // Implement exception constructors.
	{
		public FailedHttpResponseException(FailedHttpResponse failedHttpResponse) : base($"Request failed with status code {failedHttpResponse.StatusCode}")
		{
			FailedResponseData = failedHttpResponse;
		}

		///<inheritdoc cref="FailedHttpResponse"/>
		public FailedHttpResponse FailedResponseData { get; }
	}
}
