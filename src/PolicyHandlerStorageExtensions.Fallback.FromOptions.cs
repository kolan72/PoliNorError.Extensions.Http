using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	public static partial class PolicyHandlerStorageExtensions
	{
		/// <summary>
		/// Adds a handler based on a <see cref="FallbackPolicy"/> to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="func">A synchronous fallback function that returns an <see cref="System.Net.Http.HttpResponseMessage"/>.</param>
		/// <param name="configure">Delegate for configuring <see cref="FallbackPolicyOptions"/>.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage>(
			this IPolicyHandlerStorage<TStorage> storage,
			Func<CancellationToken, HttpResponseMessage> func,
			Action<FallbackPolicyOptions> configure = null) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			var options = new FallbackPolicyOptions();
			configure?.Invoke(options);

			return storage.AddFallbackHandler(func, options);
		}

		/// <summary>
		/// Adds a handler based on a <see cref="FallbackPolicy"/> to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="func">A synchronous fallback function that returns an <see cref="System.Net.Http.HttpResponseMessage"/>.</param>
		/// <param name="options"><see cref="FallbackPolicyOptions"/>.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage>(
			this IPolicyHandlerStorage<TStorage> storage,
			Func<CancellationToken, HttpResponseMessage> func,
			FallbackPolicyOptions options) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddFallbackHandler(new Fallback(func), options);
		}

		/// <summary>
		/// Adds a handler based on a <see cref="FallbackPolicy"/> to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="func">An asynchronous fallback function that returns an <see cref="System.Net.Http.HttpResponseMessage"/>.</param>
		/// <param name="configure">Delegate for configuring <see cref="FallbackPolicyOptions"/>.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage>(
			this IPolicyHandlerStorage<TStorage> storage,
			Func<CancellationToken, Task<HttpResponseMessage>> func,
			Action<FallbackPolicyOptions> configure = null) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			var options = new FallbackPolicyOptions();
			configure?.Invoke(options);

			return storage.AddFallbackHandler(func, options);
		}

		/// <summary>
		/// Adds a handler based on a <see cref="FallbackPolicy"/> to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="func">An asynchronous fallback function that returns an <see cref="System.Net.Http.HttpResponseMessage"/>.</param>
		/// <param name="options"><see cref="FallbackPolicyOptions"/>.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage>(
			this IPolicyHandlerStorage<TStorage> storage,
			Func<CancellationToken, Task<HttpResponseMessage>> func,
			FallbackPolicyOptions options) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			if (options is null)
				throw new ArgumentNullException(nameof(options));

			var bep = new BulkErrorProcessor();
			if (!(options.ConfigureErrorProcessing is null))
			{
				options.ConfigureErrorProcessing(bep);
			}

			var res = new FallbackPolicy(bep).WithAsyncFallbackFunc(func);

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
