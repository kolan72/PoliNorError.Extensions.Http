using System;

namespace PoliNorError.Extensions.Http
{
	public static partial class PolicyHandlerStorageExtensions
	{
		/// <summary>
		/// Adds a handler based on a RetryPolicy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="retryPolicy">Retry policy.</param>
		/// <returns></returns>
		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, RetryPolicy retryPolicy) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(retryPolicy);
		}

		/// <summary>
		/// Adds a handler based on a RetryPolicy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="retryPolicyFactory">Delegate to create a Retry policy.</param>
		/// <returns></returns>
		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, Func<IServiceProvider, RetryPolicy> retryPolicyFactory) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(retryPolicyFactory);
		}

		/// <summary>
		/// Adds a handler based on a RetryPolicy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage, TContext}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <typeparam name="TContext">Type of overall context.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="retryPolicyFactory">Delegate to create a Retry policy.</param>
		/// <returns></returns>
		public static TStorage AddRetryHandler<TStorage, TContext>(this IPolicyHandlerStorage<TStorage, TContext> storage, Func<TContext, IServiceProvider, RetryPolicy> retryPolicyFactory) where TStorage : IPolicyHandlerStorage<TStorage, TContext>
		{
			return storage.AddPolicyHandler(retryPolicyFactory);
		}

		/// <summary>
		/// Adds a handler based on a FallbackPolicy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="fallbackPolicy">Fallback policy.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, FallbackPolicyBase fallbackPolicy) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(fallbackPolicy);
		}

		/// <summary>
		/// Adds a handler based on a Fallback Policy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/>.</param>
		/// <param name="fallbackPolicyFactory">Delegate to create a Fallback policy.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, Func<IServiceProvider, FallbackPolicyBase> fallbackPolicyFactory) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(fallbackPolicyFactory);
		}

		/// <summary>
		/// Adds a handler based on a Fallback Policy to a pipeline builder
		/// that implements the <see cref="IPolicyHandlerStorage{TStorage, TContext}"/> interface.
		/// </summary>
		/// <typeparam name="TStorage">Storage type for <see cref="System.Net.Http.DelegatingHandler"/>.</typeparam>
		/// <typeparam name="TContext">Type of overall context.</typeparam>
		/// <param name="storage">Storage for <see cref="System.Net.Http.DelegatingHandler"/></param>
		/// <param name="fallbackPolicyFactory">Delegate to create a Fallback policy.</param>
		/// <returns></returns>
		public static TStorage AddFallbackHandler<TStorage, TContext>(this IPolicyHandlerStorage<TStorage, TContext> storage, Func<TContext, IServiceProvider, FallbackPolicyBase> fallbackPolicyFactory) where TStorage : IPolicyHandlerStorage<TStorage, TContext>
		{
			return storage.AddPolicyHandler(fallbackPolicyFactory);
		}
	}
}
