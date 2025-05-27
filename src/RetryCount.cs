namespace PoliNorError.Extensions.Http
{
	public sealed class RetryCount
	{
		private const int DEFAULT_RETRY_COUNT = 1;

		private const int REAL_INFINITE_RETRY_COUNT = int.MaxValue - 1;

		public static RetryCount Infinite() => new RetryCount() { IsInfinite = true };

		public static RetryCount FromCount(int retryCount) => new RetryCount() { Count = CorrectRetries(retryCount) };

		private RetryCount() { }

		public bool IsInfinite { get; private set; }

		public int Count { get; private set; }

		private static int CorrectRetries(int retryCount)
		{
			if (retryCount > 0)
			{
				return retryCount == int.MaxValue ? REAL_INFINITE_RETRY_COUNT : retryCount;
			}
			else
			{
				return DEFAULT_RETRY_COUNT;
			}
		}

		public static implicit operator RetryCount(int count) => FromCount(count);
	}
}
