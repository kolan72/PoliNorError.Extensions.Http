using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace PoliNorError.Extensions.Http
{
	internal class HttpPolicyResultHandler
	{
		private readonly Action<PolicyResult<HttpResponseMessage>, CancellationToken> _syncHandler;
		private readonly Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task> _asyncHandler;
		private readonly bool _isAsync;

		public HttpPolicyResultHandler(Action<PolicyResult<HttpResponseMessage>, CancellationToken> syncHandler)
		{
			_syncHandler = syncHandler;
		}

		public HttpPolicyResultHandler(Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task> asyncHandler)
		{
			_asyncHandler = asyncHandler;
			_isAsync = true;
		}

		internal void AttachTo(RetryPolicy retryPolicy)
		{
			if (_isAsync)
			{
				retryPolicy.AddPolicyResultHandler(_asyncHandler);
			}
			else
			{
				retryPolicy.AddPolicyResultHandler(_syncHandler);
			}
		}
	}
}
