using System;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents an exception thrown by any <see cref="DelegatingHandler "/> in the <see cref="Pipeline"/> on failure.
	/// </summary>
#pragma warning disable RCS1194 // Implement exception constructors.
	public class HttpPolicyResultException : Exception
#pragma warning restore RCS1194 // Implement exception constructors.
	{
		private PolicyResult<HttpResponseMessage> _innerMostPolicyResult;

		/// <summary>
		/// Creates <see cref="HttpPolicyResultException"/> with inner exception from <paramref name="policyResult"/>.UnprocessedError.
		/// </summary>
		/// <param name="policyResult">PolicyResult</param>
		/// <param name="thrownByFinalHandler">Indicates whether this exception was thrown by the final Policy handler relative to the response.</param>
		internal HttpPolicyResultException(PolicyResult<HttpResponseMessage> policyResult, bool thrownByFinalHandler = false)
			: base(policyResult.UnprocessedError?.Message,
							GetFirstNonHttpPolicyResultExceptionUnprocessedError(policyResult)
				  )
		{
			PolicyResult = policyResult;
			ThrownByFinalHandler = thrownByFinalHandler;
			FailedResponseData = (InnerException as FailedHttpResponseException)?.FailedResponseData;

			HasFailedResponse = !(FailedResponseData is null);
		}

		public override string Message => PolicyResult.IsCanceled ? "The operation was canceled" : InnerException?.Message;

		/// <summary>
		/// Specifies the <see cref="PolicyResult{HttpResponseMessage}"/> result that is produced by a policy that belongs to the DelegatingHandler that throws this exception.
		/// </summary>
		public PolicyResult<HttpResponseMessage> PolicyResult { get; }

		/// <summary>
		/// Specifies the <see cref="PolicyResult{HttpResponseMessage}"/> result produced by a policy of the final handler or by a handler in the pipeline that throws its own exception.
		/// </summary>
		public PolicyResult<HttpResponseMessage> InnermostPolicyResult
		{
			get
			{
				_innerMostPolicyResult = _innerMostPolicyResult ?? GetInnermostPolicyResult();
				return _innerMostPolicyResult;
			}
		}

		/// <summary>
		/// Indicates whether the execution was canceled.
		/// </summary>
		public bool IsCanceled => PolicyResult.IsCanceled;

		/// <summary>
		/// Indicates whether the filter for the original exception was satisfied.
		/// </summary>
		public bool IsErrorExpected => InnermostPolicyResult?.ErrorFilterUnsatisfied == false;

		/// <summary>
		/// Indicates whether this exception was thrown by the final Policy handler of pipeline relative to the response.
		/// It will be equal to false for the next <see cref="DelegatingHandler"/>s exceptions in the <see cref="Pipeline"/>.
		/// </summary>
		internal bool ThrownByFinalHandler { get; }

		/// <summary>
		/// Returns <see langword="true"/> when <see cref="FailedResponseData"/> is not null.
		/// </summary>
		public bool HasFailedResponse { get; }

		///<inheritdoc cref="FailedHttpResponse"/>
		public FailedHttpResponse FailedResponseData
		{
			get;
		}

		private static Exception GetFirstNonHttpPolicyResultExceptionUnprocessedError(PolicyResult<HttpResponseMessage> pr)
		{
			var currentPr = pr;
			while (currentPr.UnprocessedError is HttpPolicyResultException httpException)
			{
				currentPr = httpException.PolicyResult;
			}
			return currentPr.UnprocessedError;
		}

		private PolicyResult<HttpResponseMessage> GetInnermostPolicyResult()
		{
			if (ThrownByFinalHandler)
			{
				return PolicyResult;
			}

			var currentResult = PolicyResult;
			while (currentResult?.UnprocessedError?.GetType() == typeof(HttpPolicyResultException))
			{
				currentResult = ((HttpPolicyResultException)currentResult.UnprocessedError).PolicyResult;
			}
			return currentResult;
		}
	}
}
