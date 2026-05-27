using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.DependencyInjection;
using PoliNorError.Extensions.Http;
using System;
using System.Linq;
using System.Net.Http;

namespace Shared.Policies
{
	/// <summary>
	/// Policy builder for the final retry policy used in Cat API HTTP client.
	/// </summary>
	public class FinalRetryPolicyBuilder : IPolicyBuilder<FinalRetryPolicyBuilder>
	{
		private readonly ILogger _logger;

		public FinalRetryPolicyBuilder(ILogger logger)
		{
			_logger = logger;
		}

		public IPolicyBase Build()
		{
			const string policyName = "FinalHandlerAskCatRetryPolicy";
			return new RetryPolicy(3)
				.WithPolicyName(policyName)
				.WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
				{
					_logger.LogError(ex,
						"Policy {PolicyName} handled an exception on attempt {Attempt}:",
						policyName,
						pi.GetRetryCount() + 1);
					if (ex is FailedHttpResponseException failedException)
					{
						_logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}", failedException.FailedResponseData.StatusCode);
					}
				})
				.AddPolicyResultHandler<HttpResponseMessage>(pr =>
				{
					if (pr.IsPolicySuccess)
						_logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
					else if (pr.IsFailed)
					{
						_logger.LogWarning("{Errors} exceptions were thrown during handling by {PolicyName}.",
							pr.Errors.Count(),
							pr.PolicyName);
						if (pr.UnprocessedError != null)
						{
							_logger.LogError(pr.UnprocessedError,
								"UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
								policyName);
						}
					}
				})
				.WithWait(TimeSpan.FromMilliseconds(1000));
		}
	}
}
