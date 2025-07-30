using System;

namespace PoliNorError.Extensions.Http
{
	internal static class RetryAfterHeaderParser
	{
        public static TimeSpan GetTime(Exception exception)
		{
			var time = GetTimeInner();
			return time < TimeSpan.Zero ? TimeSpan.Zero : time;

			TimeSpan GetTimeInner()
			{
				switch (exception)
				{
					case FailedHttpResponseException fe when !(fe.FailedResponseData.ResponseHeaders.RetryAfter is null):
						{
							if (fe.FailedResponseData.ResponseHeaders.RetryAfter.Date.HasValue)
							{
								return fe.FailedResponseData.ResponseHeaders.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
							}

							return fe.FailedResponseData.ResponseHeaders.RetryAfter.Delta ?? TimeSpan.Zero;
						}
					default:
						return TimeSpan.Zero;
				}
			}
		}
	}
}
