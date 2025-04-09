using System;

namespace PoliNorError.Extensions.Http
{
	public static class PolicyHandlerStorageExtensions
	{
		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, RetryPolicy retryPolicy) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(retryPolicy);
		}

		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, Func<IServiceProvider, RetryPolicy> retryPolicyFactory) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(retryPolicyFactory);
		}

		public static TStorage AddRetryHandler<TStorage, TContext>(this IPolicyHandlerStorage<TStorage, TContext> storage, Func<TContext, IServiceProvider, RetryPolicy> retryPolicyFactory) where TStorage : IPolicyHandlerStorage<TStorage, TContext>
		{
			return storage.AddPolicyHandler(retryPolicyFactory);
		}
	}
}
