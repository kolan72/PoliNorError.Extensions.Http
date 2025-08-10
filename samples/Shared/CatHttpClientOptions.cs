namespace Shared
{
	public class CatHttpClientOptions
	{
		public const string CatHttpClient = "CatHttpClient";
		public string BaseUri { get; init; }
		public int Timeout { get; init; }
	}
}
