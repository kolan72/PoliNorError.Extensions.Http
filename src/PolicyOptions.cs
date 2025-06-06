using System;

namespace PoliNorError.Extensions.Http
{
	public class PolicyOptions
	{
		public Action<IHttpPolicyResultHandlers> ConfigurePolicyResultHandling { get; set; }
		public Action<IBulkErrorProcessor> ConfigureErrorProcessing { get; set; }
	}
}
