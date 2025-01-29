using System;

namespace Shared
{
	public static class Utils
	{
		public static class Randomizer
		{
			public static int Next() => _rnd.Next(4);
			private static readonly Random _rnd = new();
		}
	}
}
