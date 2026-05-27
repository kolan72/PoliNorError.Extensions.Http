using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.DependencyInjection;
using PoliNorError.Extensions.Http;
using Spectre.Console;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace Shared.Policies
{
	/// <summary>
	/// Policy builder for the outer retry policy used in Cat API HTTP client.
	/// </summary>
	public class OuterRetryPolicyBuilder : IPolicyBuilder<OuterRetryPolicyBuilder>
	{
		private readonly ILogger _logger;

		public OuterRetryPolicyBuilder(ILogger logger)
		{
			_logger = logger;
		}

		public IPolicyBase Build()
		{
			const string policyName = "OuterAskCatRetryPolicy";
			return new RetryPolicy(2)
				.WithPolicyName(policyName)
				.WithErrorProcessorOf(ex =>
				{
					_logger.LogError(ex,
						"Policy {PolicyName} handled exception: {ExceptionMessage}",
						policyName, ex.Message);
				})
				.WithErrorProcessorOf((_) =>
					AnsiConsole.Status()
						.Start("Cat needs a little rest...", _ => Thread.Sleep(3000))
				)
				.AddPolicyResultHandler<HttpResponseMessage>(pr =>
				{
					if (pr.UnprocessedError != null)
					{
						_logger.LogError(pr.UnprocessedError,
							"UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
							policyName);
					}
				});
		}
	}
}
