using System.Net;

namespace PoliNorError.Extensions.Http
{
	internal static class SuccessfulStatusCodesChecker
	{
		internal static bool IsSuccessStatusCode(HttpStatusCode statusCode) => IsSuccessStatusCode((int)statusCode);

		internal static bool IsSuccessStatusCode(int statusCode)
		{
			var categorySetKey = statusCode / 100;
			return IsSuccessStatusCodeCategory(categorySetKey);
		}

		internal static bool IsSuccessStatusCodeCategory(int categorySetKey) => categorySetKey == 2;
	}
}
