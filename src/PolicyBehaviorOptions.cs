using System;

namespace PoliNorError.Extensions.Http
{
	public class PolicyBehaviorOptions
	{
		public Action<IHttpPolicyResultHandlers> ConfigurePolicyResultHandling { get; set; }
	}
}
