namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents configuration options for a retry-based policy. Inherits common
	/// policy configuration settings from <see cref="PolicyOptions"/> and provides
	/// additional settings to control retry behavior.
	/// </summary>
	public class RetryPolicyOptions : PolicyOptions
	{
		/// <summary>
		/// Gets or sets the configuration that specifies the delay between retries.
		/// </summary>
		public RetryDelay RetryDelay { get; set; }
	}
}
