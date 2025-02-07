using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace PoliNorError.Extensions.Http
{
	internal sealed class PolicyHttpMessageHandler : DelegatingHandler
	{
		private const string PreviousResponseKey = "PolicyHttpMessageHandler.PreviousResponse";

		private IPolicyBase _policy;

		private bool _isFinalHandler;

		private IHttpErrorFilter _errorsToHandle;

		private PolicyHttpMessageHandler() { }

		public static PolicyHttpMessageHandler CreateOuterHandler(IPolicyBase policy)
        {
			return new PolicyHttpMessageHandler { _policy = policy};
		}

		public static PolicyHttpMessageHandler CreateFinalHandler(IPolicyBase policy, IHttpErrorFilter errorsToHandle)
		{
			return new PolicyHttpMessageHandler {_policy = policy, _errorsToHandle = errorsToHandle, _isFinalHandler = true };
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var fn = ((Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>)SendCoreAsync).Apply(request);
			var result = await _policy.HandleAsync(fn, cancellationToken).ConfigureAwait(false);
			if (result.IsSuccess)
				return result.Result;
			if (result.IsFailed || result.IsCanceled)
			{
				throw new HttpPolicyResultException(result, _isFinalHandler);
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			if (request.Properties.TryGetValue(PreviousResponseKey, out var priorResult) && priorResult is IDisposable disposable)
			{
				request.Properties.Remove(PreviousResponseKey);
				disposable.Dispose();
			}

			var result = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			request.Properties[PreviousResponseKey] = result;
			if (!_isFinalHandler)
				return result;
			return await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(result, _errorsToHandle).ConfigureAwait(false);
		}
	}
}
