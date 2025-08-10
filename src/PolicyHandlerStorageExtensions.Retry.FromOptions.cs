using System;

namespace PoliNorError.Extensions.Http
{
	public static partial class PolicyHandlerStorageExtensions
	{
		private static readonly Func<int, RetryPolicyOptions, IBulkErrorProcessor, RetryPolicy> _retryPolicyCreator = (rc, rpo, bep) => new RetryPolicy(rc, bep, retryDelay: rpo.RetryDelay);

		/// <summary>
		/// Adds a handler based on a RetryPolicy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="retryCount">Number of retries.</param>
		/// <param name="options"><see cref="RetryPolicyOptions"/>.</param>
		/// <returns></returns>
		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, int retryCount, RetryPolicyOptions options) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddRetryHandler(_retryPolicyCreator.Apply(retryCount), options);
		}

		/// <summary>
		/// Adds a handler based on a RetryPolicy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="retryCount">Number of retries.</param>
		/// <param name="configure">Delegate for configuring <see cref="RetryPolicyOptions"/>.</param>
		/// <returns></returns>
		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, int retryCount, Action<RetryPolicyOptions> configure = null) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			var options = new RetryPolicyOptions();
			configure?.Invoke(options);

			return storage.AddRetryHandler(retryCount, options);
		}

		internal static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, Func<RetryPolicyOptions, IBulkErrorProcessor, RetryPolicy> func, RetryPolicyOptions options) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			if (options is null)
				throw new ArgumentNullException(nameof(options));

			var bep = new BulkErrorProcessor();
			if (!(options.ConfigureErrorProcessing is null))
			{
				options.ConfigureErrorProcessing(bep);
			}

			var res = func(options, bep);

			if (options.ProcessRetryAfterHeader)
			{
				res.WithRetryAfterHeaderWait();
			}

			if (!(options.ConfigurePolicyResultHandling is null))
			{
				var handlers = new HttpPolicyResultHandlers();
				options.ConfigurePolicyResultHandling(handlers);
				handlers.AttachTo(res);
			}

			if (!(options.ConfigureErrorFilter is null))
			{
				res.AddErrorFilter(options.ConfigureErrorFilter);
			}

			if (!(options.PolicyName is null))
			{
				res.WithPolicyName(options.PolicyName);
			}
			return storage.AddPolicyHandler(res);
		}
	}
}
