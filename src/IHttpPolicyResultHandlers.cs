using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Defines a fluent interface for registering synchronous and asynchronous handlers
	/// to process the result of an HTTP policy execution.
	/// </summary>
	public interface IHttpPolicyResultHandlers
	{
		/// <summary>
		/// Adds a synchronous policy result handler.
		/// </summary>
		/// <param name="syncHandler">A delegate that processes the <see cref="PolicyResult{HttpResponseMessage}" /> synchronously.</param>
		/// <returns>The current <see cref="IHttpPolicyResultHandlers"/> instance to enable fluent chaining.</returns>
		/// <returns>
		/// The <see cref="IHttpPolicyResultHandlers"/> instance for method chaining.
		/// </returns>
		IHttpPolicyResultHandlers AddHandler(Action<PolicyResult<HttpResponseMessage>, CancellationToken> syncHandler);

		/// <summary>
		/// Adds an asynchronous policy result handler.
		/// </summary>
		/// <param name="asyncHandler">A delegate that processes the <see cref="PolicyResult{HttpResponseMessage}" /> asynchronously.</param>
		/// <returns>
		/// The <see cref="IHttpPolicyResultHandlers"/> instance for method chaining.
		/// </returns>
		IHttpPolicyResultHandlers AddHandler(Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task> asyncHandler);
	}

	internal class HttpPolicyResultHandlers : IHttpPolicyResultHandlers
	{
		private readonly List<IHttpPolicyResultHandler> _hanlders = new List<IHttpPolicyResultHandler>();

		public IHttpPolicyResultHandlers AddHandler(Action<PolicyResult<HttpResponseMessage>, CancellationToken> syncHandler)
		{
			if (syncHandler == null)
				throw new ArgumentNullException(nameof(syncHandler));

			var handler = new SyncHttpPolicyResultHandler(syncHandler);
			_hanlders.Add(handler);
			return this;
		}

		public IHttpPolicyResultHandlers AddHandler(Func<PolicyResult<HttpResponseMessage>, CancellationToken, Task> asyncHandler)
		{
			if (asyncHandler == null)
				throw new ArgumentNullException(nameof(asyncHandler));

			var handler = new AsyncHttpPolicyResultHandler(asyncHandler);
			_hanlders.Add(handler);
			return this;
		}

		internal void AttachTo(RetryPolicy retryPolicy)
		{
			foreach (var handler in _hanlders)
			{
				handler.AttachTo(retryPolicy);
			}
		}
	}
}
