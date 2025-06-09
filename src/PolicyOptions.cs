using System;

namespace PoliNorError.Extensions.Http
{
	public class PolicyOptions
	{
		public string PolicyName { get; set; }
		public Action<IHttpPolicyResultHandlers> ConfigurePolicyResultHandling { get; set; }
		public Action<IBulkErrorProcessor> ConfigureErrorProcessing { get; set; }
		public Func<IEmptyCatchBlockFilter, NonEmptyCatchBlockFilter> ConfigureErrorFilter { get; set; }
	}
}
