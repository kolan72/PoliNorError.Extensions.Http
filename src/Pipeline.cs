using System;
using System.Collections.Generic;
using System.Net.Http;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Represents outgoing request pipeline for <see cref="HttpClient"/>. The last handler in the pipeline will be the top handler for an inbound response message.
	/// </summary>
	public class Pipeline
	{
		internal Pipeline(List<Func<IServiceProvider, DelegatingHandler>> handlerFuncsStore)
		{
			HandlerChain = handlerFuncsStore;
		}

		/// <summary>
		/// Chain of <see cref="Func{IServiceProvider, DelegatingHandler}"/>
		/// </summary>
		public IEnumerable<Func<IServiceProvider, DelegatingHandler>> HandlerChain { get; }
	}
}
