using Microsoft.Extensions.Logging;
using PoliNorError;
using PoliNorError.Extensions.Http;
using Spectre.Console;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
	public static class CatPolicies
	{
		public static RetryPolicy GetFinalHandlerRetryPolicy(ILogger logger)
		{
			const string policyName = "FinalHandlerAskCatRetryPolicy";
			return new RetryPolicy(3)
							.WithPolicyName(policyName)
							.WithErrorProcessorOf((Exception ex, ProcessingErrorInfo pi) =>
								{
									logger.LogError(ex, 
													"Policy {PolicyName} processed exception on {Attempt} attempt:", 
													policyName, 
													((RetryProcessingErrorInfo)pi).RetryCount + 1);
									if (ex is FailedHttpResponseException failedException)
									{
										logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}", failedException.FailedResponseData.StatusCode);
									}
								})
							.AddPolicyResultHandler<HttpResponseMessage>(pr =>
							{
								if (pr.IsPolicySuccess)
									logger.LogInformation("Policy '{PolicyName}' handled delegate successfully", pr.PolicyName);
								else if (pr.IsFailed)
								{
									logger.LogWarning("{Errors} exceptions were thrown during handling by '{PolicyName}'.", 
														pr.Errors.Count(), 
														pr.PolicyName);
									if (pr.UnprocessedError is not null)
									{
										logger.LogError("UnprocessedError: {UnprocessedError}", pr.UnprocessedError.Message);
									}
								}
							})
							.WithWait(TimeSpan.FromMilliseconds(1000));
		}

		public static RetryPolicy GetOuterRetryPolicy(ILogger logger)
		{
			const string policyName = "OuterAskCatRetryPolicy";
			return new RetryPolicy(2)
							   .WithPolicyName(policyName)
							   .WithErrorProcessorOf(ex => 

									logger.LogError("Policy {PolicyName} processed exception: {Message}",
													policyName, 
													ex.Message)
							   )
							   .AddPolicyResultHandler<HttpResponseMessage>(pr =>
							   {
								   if (pr.IsPolicySuccess)
									   logger.LogInformation("Policy '{PolicyName}' handled delegate successfully", pr.PolicyName);
							   })
							   .WithErrorProcessorOf((_) =>
											   AnsiConsole.Status()
												.Start("Cat needs a little rest...", _ =>
												{
													Thread.Sleep(3000);
												})
								);
		}

		public static FallbackPolicyBase GetOuterFallbackPolicy(ILogger logger, string customAnswer)
		{
			var fallbackPolicy = new FallbackPolicy()
									.WithPolicyName("CatAnswerFallbackPolicy")
									.WithAsyncFallbackFunc((_) => Task.FromResult(GetCustomFallbackCatAnswer(customAnswer)))
									.AddPolicyResultHandler<HttpResponseMessage>(pr =>
									{
										if (pr.IsPolicySuccess)
											logger.LogInformation("Policy '{PolicyName}' handled delegate successfully", pr.PolicyName);
									});
			return fallbackPolicy;
		}

		public static FallbackPolicyBase GetOuterFallbackPolicy(ILogger logger)
		{
			var fallbackPolicy = new FallbackPolicy()
									.WithPolicyName("CatAnswerFallbackPolicy")
									.WithAsyncFallbackFunc((_) => Task.FromResult(UsualFallbackCatAnswer))
									.AddPolicyResultHandler<HttpResponseMessage>(pr =>
									{
										if (pr.IsPolicySuccess)
											logger.LogInformation("Policy '{PolicyName}' handled delegate successfully", pr.PolicyName);
									});
			return fallbackPolicy;
		}

		private static HttpResponseMessage GetCustomFallbackCatAnswer(string customAnswer)
			=> new HttpResponseMessage() { Content = 
												new StringContent(JsonSerializer.Serialize(new CatResponse() 
																							{ Fact = customAnswer }), Encoding.UTF8, "application/json") };

		private static HttpResponseMessage UsualFallbackCatAnswer 
			=> new HttpResponseMessage() {	Content = new StringContent(JsonSerializer.Serialize(new CatResponse() { Fact = "Meow!" }), 
											Encoding.UTF8, 
											"application/json") };

	}
}
