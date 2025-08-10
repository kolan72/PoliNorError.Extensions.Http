using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;

namespace Shared
{
	public static class HttpClientConfiguration
	{
		internal const string CatClientHttpClientName = "catClient";

		public static IHttpClientBuilder AddCatHttpClient(this IServiceCollection services)
		{
			return services.AddHttpClient<IAskCatService, AskCatService>(ConfigureClient);
		}

		public static IHttpClientBuilder AddNamedCatHttpClient(this IServiceCollection services)
		{
			return services.AddHttpClient(CatClientHttpClientName, ConfigureClient);
		}

		private static readonly Action<IServiceProvider, HttpClient> ConfigureClient = (sp, client) =>
		{
			var settings = sp
				.GetRequiredService<IOptions<CatHttpClientOptions>>().Value;
			client.BaseAddress = new Uri(settings.BaseUri);
			client.Timeout = TimeSpan.FromMilliseconds(settings.Timeout);
		};
	}
}
