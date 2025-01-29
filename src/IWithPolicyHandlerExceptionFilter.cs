using System;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This interface is primarily for internal use.
	/// </summary>
	public interface IWithPolicyHandlerExceptionFilter<out T> where T : IWithPolicyHandlerExceptionFilter<T>
	{
		/// <summary>
		/// Adds an exception type to be included in the filter by the policy of a <see cref="System.Net.Http.DelegatingHandler"/>.
		/// </summary>
		/// <typeparam name="TException">Exception type</typeparam>
		/// <param name="func">Predicate to filter <typeparamref name="TException"/> exception</param>
		/// <returns></returns>
		T IncludeException<TException>(Func<TException, bool> func = null) where TException : Exception;
	}
}
