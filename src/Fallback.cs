using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	public class Fallback
	{
		public Fallback(Func<CancellationToken, HttpResponseMessage> func)
		{
			Func = func ?? throw new ArgumentNullException(nameof(func));
		}

		internal Func<CancellationToken, HttpResponseMessage> Func { get; set; }

		public static implicit operator Func<CancellationToken, Task<HttpResponseMessage>>(Fallback fallback)
			=> ct => Task.FromResult(fallback.Func(ct));
	}
}
