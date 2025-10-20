using System;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Provides configuration options for customizing policy behavior in error processing and result handling.
	/// </summary>
	public class PolicyOptions
	{
		/// <summary>
		/// Gets or sets the name of the policy.
		/// </summary>
		public string PolicyName { get; set; }

		/// <summary>
		/// Gets or sets a delegate to add PolicyResult&lt;HttpResponseMessage&gt; handlers.
		/// </summary>
		public Action<IHttpPolicyResultHandlers> ConfigurePolicyResultHandling { get; set; }

		/// <summary>
		/// Gets or sets the action that configures error processing by adding an <see cref=“IErrorProcessor”/> to a policy.
		/// </summary>
		public Action<BulkErrorProcessor> ConfigureErrorProcessing { get; set; }

		/// <summary>
		/// Gets or sets the function that configures the error filter for a policy.
		/// </summary>
		public Func<IEmptyCatchBlockFilter, NonEmptyCatchBlockFilter> ConfigureErrorFilter { get; set; }
	}
}
