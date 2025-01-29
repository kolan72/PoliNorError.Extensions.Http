using Microsoft.Extensions.DependencyInjection;
using System;

namespace PoliNorError.Extensions.Http.Tests
{
	internal static class ServiceCollectionExtensions
	{
		public static IHttpClientBuilder AddFakeHttpClient(this ServiceCollection services)
		{
			return services.AddHttpClient("my-httpclient", client => client.BaseAddress = new Uri("http://any.localhost"));
		}
	}
}
