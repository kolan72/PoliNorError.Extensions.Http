using System;
using System.Diagnostics;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// A lightweight, allocation-free stopwatch for measuring elapsed time.
	/// Uses ValueType to avoid heap allocations compared to System.Diagnostics.Stopwatch.
	/// </summary>
	internal readonly struct ValueStopwatch
	{
		private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

		private readonly long _startTimestamp;

		private ValueStopwatch(long startTimestamp)
		{
			_startTimestamp = startTimestamp;
		}

		/// <summary>
		/// Gets a value indicating whether the stopwatch is running.
		/// </summary>
		public bool IsActive => _startTimestamp != 0;

		/// <summary>
		/// Creates and starts a new ValueStopwatch.
		/// </summary>
		public static ValueStopwatch StartNew() => new ValueStopwatch(Stopwatch.GetTimestamp());

		/// <summary>
		/// Gets the elapsed time as a TimeSpan.
		/// </summary>
		public TimeSpan Elapsed
		{
			get
			{
				if (!IsActive)
					return TimeSpan.Zero;

				var end = Stopwatch.GetTimestamp();
				var timestampDelta = end - _startTimestamp;
				var ticks = (long)(TimestampToTicks * timestampDelta);
				return new TimeSpan(ticks);
			}
		}

		/// <summary>
		/// Gets the elapsed time in milliseconds.
		/// </summary>
		public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;

		/// <summary>
		/// Stops the stopwatch (for semantic clarity; actual stopping is implicit).
		/// </summary>
		public void Stop()
		{
			// No-op: stopping is implicit when reading Elapsed
			// This method exists for API consistency with Stopwatch
		}
	}
}
