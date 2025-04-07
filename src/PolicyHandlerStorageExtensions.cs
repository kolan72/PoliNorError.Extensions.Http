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
	}
}
