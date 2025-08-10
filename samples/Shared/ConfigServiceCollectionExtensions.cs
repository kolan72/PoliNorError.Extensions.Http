using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Shared
{
	public static class ConfigServiceCollectionExtensions
	{
		public static IServiceCollection AddConfig(
		   this IServiceCollection services)
		{
			IConfiguration configuration = new ConfigurationBuilder()
							 .SetBasePath(Directory.GetCurrentDirectory())
							 .AddJsonFile("appSettings.json", false)
							 .Build();

			return services.Configure<CatHttpClientOptions>(configuration.GetSection("CatHttpClient"));
		}
	}
}
