namespace PoliNorError.Extensions.Http
{
	public static class PolicyHandlerStorageExtensions
	{
		public static TStorage AddRetryHandler<TStorage>(this IPolicyHandlerStorage<TStorage> storage, RetryPolicy retryPolicy) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			return storage.AddPolicyHandler(retryPolicy);
		}
	}
}
