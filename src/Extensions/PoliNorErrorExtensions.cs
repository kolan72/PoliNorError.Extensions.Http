namespace PoliNorError.Extensions.Http
{
	internal static class PoliNorErrorExtensions
	{
		public static RetryPolicy WithRetryAfterHeaderWait(this RetryPolicy retryPolicy)
		{
			return retryPolicy.WithWait((_, ex) => RetryAfterHeaderParser.GetTime(ex));
		}
	}
}
