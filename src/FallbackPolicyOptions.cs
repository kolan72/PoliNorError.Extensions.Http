using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents configuration options for a fallback-based policy. Inherits common
	/// policy configuration settings from <see cref="PolicyOptions"/> and provides
	/// additional settings to control fallback behavior.
	/// </summary>
	public class FallbackPolicyOptions : PolicyOptions
	{
		public Func<CancellationToken, Task<HttpResponseMessage>> Fallback { get; set; }
	}
}
