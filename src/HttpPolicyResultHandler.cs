using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using PoliNorError.Extensions.PolicyResultHandling;

namespace PoliNorError.Extensions.Http
{
	internal interface IHttpPolicyResultHandler
	{
		void AttachTo(Policy policy);
	}

	internal class SyncHttpPolicyResultHandler : IHttpPolicyResultHandler
	{
		private readonly Action<PolicyResult<HttpResponseMessage>, CancellationToken> _syncHandler;

		public SyncHttpPolicyResultHandler(Action<PolicyResult<HttpResponseMessage>, CancellationToken> syncHandler)
		{
			_syncHandler = syncHandler;
		}

		public void AttachTo(Policy policy)
		{
			policy.AddHandlerForPolicyResult(_syncHandler);
		}
	}

	internal class NotCancelableSyncHttpPolicyResultHandler : IHttpPolicyResultHandler
	{
		private readonly Action<PolicyResult<HttpResponseMessage>> _syncHandler;

        public NotCancelableSyncHttpPolicyResultHandler(Action<PolicyResult<HttpResponseMessage>> syncHandler)
        {
			_syncHandler = syncHandler;
		}

        public void AttachTo(Policy policy)
		{
			policy.AddHandlerForPolicyResult(_syncHandler);
		}
	}

	internal class AsyncHttpPolicyResultHandler : IHttpPolicyResultHandler
	{
		private readonly Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task> _asyncHandler;

		public AsyncHttpPolicyResultHandler(Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task> asyncHandler)
		{
			_asyncHandler = asyncHandler;
		}

		public void AttachTo(Policy policy)
		{
			policy.AddHandlerForPolicyResult(_asyncHandler);
		}
	}

	internal class NotCancelableAsyncHttpPolicyResultHandler : IHttpPolicyResultHandler
	{
		private readonly Func<PolicyResult<HttpResponseMessage>, Task> _asyncHandler;

        public NotCancelableAsyncHttpPolicyResultHandler(Func<PolicyResult<HttpResponseMessage>, Task> asyncHandler)
        {
			_asyncHandler = asyncHandler;
		}

		public void AttachTo(Policy policy)
		{
			policy.AddHandlerForPolicyResult(_asyncHandler);
		}
	}
}
