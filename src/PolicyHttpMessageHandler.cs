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
			// Create an Activity only when a listener is attached (zero-alloc no-op otherwise).
			using (var activity = StartPipelineActivity())
			{
				var fn = ((Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>)SendCoreAsync).Apply(request);

				var result = await _policy.HandleAsync(fn, cancellationToken).ConfigureAwait(false);

				if (result.IsSuccess)
				{
					SetResultTag(activity, "success");
					return result.Result;
				}

				if (result.IsFailed || result.IsCanceled)
				{
					SetResultTag(activity, result.IsCanceled ? "canceled" : "failed");
					if (result.IsCanceled)
						activity?.SetStatus(ActivityStatusCode.Error, "Operation canceled");
					else
						activity?.SetStatus(ActivityStatusCode.Error, result.UnprocessedError?.Message);

					throw new HttpPolicyResultException(result, _isFinalHandler);
				}
				else
				{
					activity?.SetStatus(ActivityStatusCode.Error, "Unexpected policy result state");
					throw new NotImplementedException();
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
				activity.SetTag("pipeline.is_final_handler", _isFinalHandler);
				activity.SetTag("pipeline.policy.type", _policy?.GetType().Name ?? "Unknown");
			}

			return activity;
		}

		private static void SetResultTag(Activity activity, string result)
		{
			if (activity != null)
			{
				activity.SetTag("pipeline.result", result);
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
