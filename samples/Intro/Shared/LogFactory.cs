using Microsoft.Extensions.Logging;
using Vertical.SpectreLogger;
using Vertical.SpectreLogger.Options;
using Vertical.SpectreLogger.Rendering;

namespace Shared
{
	public static class LogFactory
	{
		public static ILogger CreateLogger()
		{
			var loggerFactory = LoggerFactory.Create(opt => opt
																.AddFilter("System.Net.Http", l => l > LogLevel.Information)
																.AddSpectreConsole(specLogger =>
																{
																	specLogger.ConfigureProfile(LogLevel.Error, profile =>
																	{
																		profile.ConfigureOptions<ExceptionRenderer.Options>(opt =>
																		{
																			opt.MaxStackFrames = 1;

																			// Show parameter types but not names
																			opt.ShowParameterTypes = true;
																			opt.ShowParameterNames = false;
																			opt.ShowSourcePaths = false;
																			opt.ShowSourceLocations = false;
																			// Show inner exceptions
																			opt.UnwindInnerExceptions = false;
																		});
																		// Show the exception class name only
																		profile.AddTypeFormatter<ExceptionRenderer.ExceptionNameValue>((_, arg) => arg.Value.Name);
																	});
																})
																);
			return loggerFactory.CreateLogger("Program");
		}
	}
}
