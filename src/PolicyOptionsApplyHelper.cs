using System;
using PoliNorError;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Shared logic for applying <see cref="PolicyOptions"/> configuration
	/// (error processing, result handling, error filter, policy name) to a policy.
	/// Eliminates duplication between the retry and fallback "FromOptions" paths.
	/// </summary>
	internal static class PolicyOptionsApplyHelper
	{
		/// <summary>
		/// Creates a <see cref="BulkErrorProcessor"/> and invokes
		/// <see cref="PolicyOptions.ConfigureErrorProcessing"/> if set.
		/// </summary>
		internal static BulkErrorProcessor CreateConfiguredBulkErrorProcessor(PolicyOptions options)
		{
			var bep = new BulkErrorProcessor();
			if (!(options.ConfigureErrorProcessing is null))
			{
				options.ConfigureErrorProcessing(bep);
			}
			return bep;
		}

		/// <summary>
		/// Applies the remaining shared configuration from <paramref name="options"/>
		/// to a policy: result handling, error filter, and policy name.
		/// Call this after the policy has been created.
		/// </summary>
		internal static void ApplySharedConfiguration<T>(PolicyOptions options, T policy)
			where T : Policy, ICanAddErrorFilter<T>
		{
			if (!(options.ConfigurePolicyResultHandling is null))
			{
				var handlers = new HttpPolicyResultHandlers();
				options.ConfigurePolicyResultHandling(handlers);
				handlers.AttachTo(policy);
			}

			if (!(options.ConfigureErrorFilter is null))
			{
				policy.AddErrorFilter(options.ConfigureErrorFilter);
			}

			if (!(options.PolicyName is null))
			{
				policy.WithPolicyName(options.PolicyName);
			}
		}
	}
}
