using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// ActivitySource for distributed tracing of HTTP resilience pipeline operations.
	/// </summary>
	internal static class HttpResilienceActivitySource
	{
		/// <summary>
		/// The name of the activity source for HTTP resilience operations.
		/// </summary>
		public const string ActivitySourceName = "PoliNorError.Http";

		/// <summary>
		/// The shared ActivitySource instance for HTTP resilience operations.
		/// </summary>
		public static readonly ActivitySource Instance = new ActivitySource(ActivitySourceName);
	}

	internal sealed class PolicyHttpMessageHandler : DelegatingHandler
	{
		private const string PreviousResponseKey = "PolicyHttpMessageHandler.PreviousResponse";

		private readonly IPolicyBase _policy;
		private readonly bool _isFinalHandler;
		private readonly HttpErrorFilterCriteria _errorsToHandle;
		private readonly ILogger<PolicyHttpMessageHandler> _logger;

		private PolicyHttpMessageHandler(IPolicyBase policy, bool isFinalHandler, HttpErrorFilterCriteria errorsToHandle, ILogger<PolicyHttpMessageHandler> logger)
		{
			_policy = policy;
			_isFinalHandler = isFinalHandler;
			_errorsToHandle = errorsToHandle;
			_logger = logger;
		}

		public static PolicyHttpMessageHandler CreateOuterHandler(IPolicyBase policy, ILogger<PolicyHttpMessageHandler> logger = null)
		{
			return new PolicyHttpMessageHandler(policy, isFinalHandler: false, errorsToHandle: null, logger: logger);
		}

		public static PolicyHttpMessageHandler CreateFinalHandler(IPolicyBase policy, HttpErrorFilterCriteria errorsToHandle, ILogger<PolicyHttpMessageHandler> logger = null)
		{
			return new PolicyHttpMessageHandler(policy, isFinalHandler: true, errorsToHandle: errorsToHandle, logger: logger);
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// Start distributed tracing activity
			Activity activity = null;
			if (HttpResilienceActivitySource.Instance.HasListeners())
			{
				activity = HttpResilienceActivitySource.Instance.StartActivity(
					"HttpPolicyExecution",
					ActivityKind.Client);
				
				if (activity != null)
				{
					activity.SetTag("http.url", request.RequestUri?.ToString());
					activity.SetTag("http.method", request.Method.Method);
					activity.SetTag("policy.type", _policy.GetType().Name);
					activity.SetTag("policy.is_final", _isFinalHandler);
				}
			}

			var stopwatch = ValueStopwatch.StartNew();

			try
			{
				var fn = ((Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>)SendCoreAsync).Apply(request);
				var result = await _policy.HandleAsync(fn, cancellationToken).ConfigureAwait(false);

				stopwatch.Stop();

				if (result.IsSuccess)
				{
					// Log successful execution with TraceId and SpanId
					if (_logger != null && _logger.IsEnabled(LogLevel.Information))
					{
						var currentActivity = Activity.Current;
						if (currentActivity != null)
						{
							_logger.LogInformation(
								"HTTP request succeeded after {ElapsedMs}ms. Method={HttpMethod}, Url={HttpUrl}, Policy={PolicyType}, StatusCode={StatusCode}, TraceId={TraceId}, SpanId={SpanId}",
								stopwatch.ElapsedMilliseconds,
								request.Method.Method,
								request.RequestUri?.ToString(),
								_policy.GetType().Name,
								(int)result.Result.StatusCode,
								currentActivity.TraceId.ToString(),
								currentActivity.SpanId.ToString());
						}
						else
						{
							_logger.LogInformation(
								"HTTP request succeeded after {ElapsedMs}ms. Method={HttpMethod}, Url={HttpUrl}, Policy={PolicyType}, StatusCode={StatusCode}",
								stopwatch.ElapsedMilliseconds,
								request.Method.Method,
								request.RequestUri?.ToString(),
								_policy.GetType().Name,
								(int)result.Result.StatusCode);
						}
					}

					if (activity != null)
					{
						activity.SetTag("http.status_code", (int)result.Result.StatusCode);
						activity.SetTag("http.success", true);
						activity.SetStatus(ActivityStatusCode.Ok);
					}

					return result.Result;
				}

				// Handle failure
				stopwatch.Stop();
				var errorType = result.IsCanceled ? "Canceled" : "Failed";
				
				if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
				{
					var currentActivity = Activity.Current;
					if (currentActivity != null)
					{
						_logger.LogWarning(
							"HTTP request {ErrorType} after {ElapsedMs}ms. Method={HttpMethod}, Url={HttpUrl}, Policy={PolicyType}, IsFinalHandler={IsFinalHandler}, TraceId={TraceId}, SpanId={SpanId}",
							errorType,
							stopwatch.ElapsedMilliseconds,
							request.Method.Method,
							request.RequestUri?.ToString(),
							_policy.GetType().Name,
							_isFinalHandler,
							currentActivity.TraceId.ToString(),
							currentActivity.SpanId.ToString());
					}
					else
					{
						_logger.LogWarning(
							"HTTP request {ErrorType} after {ElapsedMs}ms. Method={HttpMethod}, Url={HttpUrl}, Policy={PolicyType}, IsFinalHandler={IsFinalHandler}",
							errorType,
							stopwatch.ElapsedMilliseconds,
							request.Method.Method,
							request.RequestUri?.ToString(),
							_policy.GetType().Name,
							_isFinalHandler);
					}
				}

				if (activity != null)
				{
					activity.SetTag("http.success", false);
					activity.SetTag("error.type", errorType);
				}

				throw new HttpPolicyResultException(result, _isFinalHandler);
			}
			catch (Exception ex)
			{
				// Only handle non-HttpPolicyResultException exceptions
				if (ex is HttpPolicyResultException)
				{
					throw;
				}

				// Unexpected exception - log with full details
				stopwatch.Stop();
				
				if (_logger != null && _logger.IsEnabled(LogLevel.Error))
				{
					var currentActivity = Activity.Current;
					if (currentActivity != null)
					{
						_logger.LogError(
							ex,
							"Unexpected error in HTTP resilience pipeline after {ElapsedMs}ms. Policy={PolicyType}, TraceId={TraceId}, SpanId={SpanId}",
							stopwatch.ElapsedMilliseconds,
							_policy.GetType().Name,
							currentActivity.TraceId.ToString(),
							currentActivity.SpanId.ToString());
					}
					else
					{
						_logger.LogError(
							ex,
							"Unexpected error in HTTP resilience pipeline after {ElapsedMs}ms. Policy={PolicyType}",
							stopwatch.ElapsedMilliseconds,
							_policy.GetType().Name);
					}
				}

				if (activity != null)
				{
					activity.SetTag("error.type", ex.GetType().Name);
					activity.SetStatus(ActivityStatusCode.Error, ex.Message);
				}
				
				throw;
			}
			finally
			{
				if (activity != null)
				{
					activity.Dispose();
				}
			}
		}

		private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			// Dispose previous response if exists (prevents memory leaks)
			if (request.Properties.TryGetValue(PreviousResponseKey, out var priorResult))
			{
				var disposable = priorResult as IDisposable;
				if (disposable != null)
				{
					request.Properties.Remove(PreviousResponseKey);
					disposable.Dispose();
				}
			}

			var result = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			// Store current response for potential retry/cleanup
			request.Properties[PreviousResponseKey] = result;

			// If not final handler, return as-is; otherwise apply error filtering
			if (!_isFinalHandler)
				return result;

			return await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(result, _errorsToHandle).ConfigureAwait(false);
		}
	}
}
