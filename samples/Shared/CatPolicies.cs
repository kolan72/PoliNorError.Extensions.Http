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
													"Policy {PolicyName} handled an exception on attempt {Attempt}:", 
													policyName,
													pi.GetRetryCount() + 1);
									if (ex is FailedHttpResponseException failedException)
									{
										logger.LogWarning(ex, "The cat's answer is error. StatusCode {StatusCode}", failedException.FailedResponseData.StatusCode);
									}
								})
							.AddPolicyResultHandler<HttpResponseMessage>(pr =>
							{
								if (pr.IsPolicySuccess)
									logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
								else if (pr.IsFailed)
								{
									logger.LogWarning("{Errors} exceptions were thrown during handling by {PolicyName}.", 
														pr.Errors.Count(), 
														pr.PolicyName);
									if (pr.UnprocessedError is not null)
									{
										logger.LogError(pr.UnprocessedError, 
														"UnprocessedError – an exception that was not handled by error processors of the {PolicyName}", 
														policyName);
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
							   {
								   logger.LogError(ex,
												   "Policy {PolicyName} handled exception: {ExceptionMessage}",
												   policyName, ex.Message);
							   })
							   .WithErrorProcessorOf((_) =>
											   AnsiConsole.Status()
												.Start("Cat needs a little rest...", _ => Thread.Sleep(3000))
								)
							   .AddPolicyResultHandler<HttpResponseMessage>(pr =>
							   {
								   if (pr.UnprocessedError is not null)
								   {
									   logger.LogError(pr.UnprocessedError, 
														"UnprocessedError – an exception that was not handled by error processors of the {PolicyName}",
														policyName);
								   }
							   });
		}

		public static FallbackPolicyBase GetOuterFallbackPolicy(ILogger logger, string customAnswer)
		{
			return new FallbackPolicy()
									.WithPolicyName("CatAnswerFallbackPolicy")
									.WithAsyncFallbackFunc((_) => Task.FromResult(GetCustomFallbackCatAnswer(customAnswer)))
									.AddPolicyResultHandler<HttpResponseMessage>(pr =>
									{
										if (pr.IsPolicySuccess)
											logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
									});
		}

		public static FallbackPolicyBase GetOuterFallbackPolicy(ILogger logger)
		{
			return new FallbackPolicy()
									.WithPolicyName("CatAnswerFallbackPolicy")
									.WithAsyncFallbackFunc((_) => Task.FromResult(UsualFallbackCatAnswer))
									.AddPolicyResultHandler<HttpResponseMessage>(pr =>
									{
										if (pr.IsPolicySuccess)
											logger.LogInformation("Policy {PolicyName} handled delegate successfully", pr.PolicyName);
									});
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
