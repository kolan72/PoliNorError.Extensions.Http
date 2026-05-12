using System;
using System.Runtime.CompilerServices;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Helper methods for throwing exceptions with optimized code generation.
	/// </summary>
	internal static class ThrowHelper
	{
		/// <summary>
		/// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.
		/// </summary>
		/// <param name="argument">The reference type argument to validate as non-null.</param>
		/// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ThrowIfNull(object argument, string paramName = null)
		{
			if (argument is null)
			{
				throw new ArgumentNullException(paramName);
			}
		}
	}
}
