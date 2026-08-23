using System;
using System.Diagnostics;
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

		private HttpErrorFilterCriteria _errorsToHandle;

		private PolicyHttpMessageHandler() { }

		public static PolicyHttpMessageHandler CreateOuterHandler(IPolicyBase policy)
		{
			return new PolicyHttpMessageHandler { _policy = policy };
		}

		public static PolicyHttpMessageHandler CreateFinalHandler(IPolicyBase policy, HttpErrorFilterCriteria errorsToHandle)
		{
			return new PolicyHttpMessageHandler { _policy = policy, _errorsToHandle = errorsToHandle, _isFinalHandler = true };
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			using (var activity = StartPipelineActivity())
			{
				try
				{
					var fn = ((Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>)SendCoreAsync).Apply(request);

					var result = await _policy.HandleAsync(fn, cancellationToken).ConfigureAwait(false);

					if (result.IsSuccess)
					{
						SetResultTag(activity, "success");
						activity?.SetStatus(ActivityStatusCode.Ok);
						return result.Result;
					}

					if (result.IsFailed || result.IsCanceled)
					{
						SetResultTag(activity, result.IsCanceled ? "canceled" : "failed");
						if (result.IsCanceled)
							activity?.SetStatus(ActivityStatusCode.Error, "Operation canceled");
						else
							activity?.SetStatus(ActivityStatusCode.Error, result.UnprocessedError?.Message);

						DisposeOrphanedPreviousResponse(request);
						throw new HttpPolicyResultException(result, _isFinalHandler);
					}
					else
					{
						activity?.SetStatus(ActivityStatusCode.Error, "Unexpected policy result state");
						DisposeOrphanedPreviousResponse(request);
						throw new NotImplementedException();
					}
				}
				catch (Exception ex) when (!(ex is HttpPolicyResultException))
				{
					// An unexpected exception escaped the policy (e.g., _policy.HandleAsync threw
					// instead of returning a PolicyResult). Make the span authoritative: record the
					// exception and mark it as errored so the trace reflects every failure mode.
					// HttpPolicyResultException is excluded - its status is set explicitly above.
					SetResultTag(activity, "faulted");
					activity?.AddException(ex);
					if (activity?.Status != ActivityStatusCode.Error)
						activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
					DisposeOrphanedPreviousResponse(request);
					throw;
				}
			}
		}

		private Activity StartPipelineActivity()
		{
			var activity = PipelineTelemetry.Source.StartActivity(
				PipelineTelemetry.PipelineOperationName,
				ActivityKind.Internal);

			if (activity != null)
			{
				activity.SetTag(PipelineTelemetry.IsFinalHandlerTag, _isFinalHandler);
				var policyType = _policy?.GetType().Name ?? "Unknown";
				activity.SetTag(PipelineTelemetry.PolicyTypeTag, policyType);

				if (_policy is IPolicyBase namedPolicy)
				{
					var policyName = namedPolicy.PolicyName;
					if (!string.IsNullOrEmpty(policyName) && policyName != policyType)
					{
						activity.SetTag(PipelineTelemetry.PolicyNameTag, policyName);
					}
				}
			}

			return activity;
		}

		private static void SetResultTag(Activity activity, string result)
		{
			if (activity != null)
			{
				activity.SetTag(PipelineTelemetry.ResultTag, result);
			}
		}

		private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			DisposeOrphanedPreviousResponse(request);

			var result = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			request.Properties[PreviousResponseKey] = result;
			if (!_isFinalHandler)
				return result;
			return await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(result, _errorsToHandle).ConfigureAwait(false);
		}

		private static void DisposeOrphanedPreviousResponse(HttpRequestMessage request)
		{
			if (request.Properties.TryGetValue(PreviousResponseKey, out var priorResult) && priorResult is IDisposable disposable)
			{
				request.Properties.Remove(PreviousResponseKey);
				disposable.Dispose();
			}
		}
	}
}
