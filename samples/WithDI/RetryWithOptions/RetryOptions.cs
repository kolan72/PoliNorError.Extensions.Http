namespace RetryWithOptions
{
	/// <summary>
	/// Configuration options for retry policy loaded from appSettings.json
	/// </summary>
	public class RetryOptions
	{
		/// <summary>
		/// Maximum number of retry attempts
		/// </summary>
		public int MaxRetryCount { get; set; } = 3;

		/// <summary>
		/// Delay between retry attempts in milliseconds
		/// </summary>
		public int DelayMilliseconds { get; set; } = 1000;

		/// <summary>
		/// Name of the retry policy for logging purposes
		/// </summary>
		public string PolicyName { get; set; } = "RetryPolicy";
	}
}
