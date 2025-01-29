using System;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// This interface represents an storage of <see cref=" System.Net.Http.DelegatingHandler"/>, primarily for internal use.
	/// </summary>
	public interface IPolicyHandlerStorage<TStorage> where TStorage : IPolicyHandlerStorage<TStorage>
	{
		/// <summary>
		/// Adds <see cref="System.Net.Http.DelegatingHandler"/> that uses policy to handle errors.
		/// </summary>
		/// <typeparam name="T">Type of policy.</typeparam>
		/// <param name="policy">Policy for handling errors.</param>
		/// <returns></returns>
		TStorage AddPolicyHandler<T>(T policy) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase;

		/// <summary>
		/// Adds <see cref="System.Net.Http.DelegatingHandler"/> that uses a policy created by <paramref name="policyFactory"/> to handle errors.
		/// </summary>
		/// <typeparam name="T">Type of policy.</typeparam>
		/// <param name="policyFactory">Delegate to create a policy.</param>
		/// <returns></returns>
		TStorage AddPolicyHandler<T>(Func<IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase;
	}

	/// <summary>
	/// This interface represents an storage of <see cref=" System.Net.Http.DelegatingHandler"/> and uses a policy factory with overall context, primarily for internal use.
	/// </summary>
	/// <typeparam name="TStorage">Type of storage.</typeparam>
	/// <typeparam name="TContext">Type of overall context.</typeparam>
	public interface IPolicyHandlerStorage<TStorage, out TContext> : IPolicyHandlerStorage<TStorage> where TStorage : IPolicyHandlerStorage<TStorage, TContext>
	{
		/// <summary>
		/// Adds <see cref="System.Net.Http.DelegatingHandler"/> that uses a policy created by <paramref name="policyFactory"/> with an overall context parameter to handle errors.
		/// </summary>
		/// <typeparam name="T">Type of policy.</typeparam>
		/// <param name="policyFactory">Delegate to create a policy.</param>
		/// <returns></returns>
		TStorage AddPolicyHandler<T>(Func<TContext, IServiceProvider, T> policyFactory) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>, IPolicyBase;
	}
}
