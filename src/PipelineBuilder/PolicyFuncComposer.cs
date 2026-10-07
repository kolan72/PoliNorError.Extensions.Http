using System;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Shared logic for composing policy factory functions with error filters
	/// and exception inclusions. Eliminates duplication between the generic
	/// and non-generic pipeline builder variants.
	/// </summary>
	internal static class PolicyFuncComposer
	{
		internal static Func<IServiceProvider, T> ApplyFinalHandlerFilter<T>(
			Func<IServiceProvider, T> previous,
			HttpErrorFilterCriteria errorsToHandle)
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
		{
			return (sp) => previous(sp).WithErrorsFilter(errorsToHandle);
		}

		internal static Func<TArg, IServiceProvider, T> ApplyFinalHandlerFilter<TArg, T>(
			Func<TArg, IServiceProvider, T> previous,
			HttpErrorFilterCriteria errorsToHandle)
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
		{
			return (arg, sp) => previous(arg, sp).WithErrorsFilter(errorsToHandle);
		}

		internal static Func<IServiceProvider, T> ApplyDefaultHttpErrorFilter<T>(Func<IServiceProvider, T> previous)
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
		{
			return (sp) => previous(sp).IncludeError<HttpPolicyResultException>();
		}

		internal static Func<TArg, IServiceProvider, T> ApplyDefaultHttpErrorFilter<TArg, T>(Func<TArg, IServiceProvider, T> previous) where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
		{
			return (arg, sp) => previous(arg, sp).IncludeError<HttpPolicyResultException>();
		}

		internal static Func<IServiceProvider, T> ApplyInnerExceptionFilter<T, TException>(
			Func<IServiceProvider, T> previous,
			Func<TException, bool> func)
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
			where TException : Exception
		{
			return (sp) => previous(sp).IncludeInnerError(func);
		}

		internal static Func<TArg, IServiceProvider, T> ApplyInnerExceptionFilter<TArg, T, TException>(
			Func<TArg, IServiceProvider, T> previous,
			Func<TException, bool> func) 
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
			where TException : Exception
		{
			return (arg, sp) => previous(arg, sp).IncludeInnerError(func);
		}

		internal static Func<IServiceProvider, T> ApplyTopLevelExceptionFilter<T, TException>(
			Func<IServiceProvider, T> previous,
			Func<TException, bool> func)
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
			where TException : Exception
		{
			return (sp) => previous(sp).IncludeError(func);
		}

		internal static Func<TArg, IServiceProvider, T> ApplyTopLevelExceptionFilter<TArg, T, TException>(
			Func<TArg, IServiceProvider, T> previous,
			Func<TException, bool> func)
			where T : IWithErrorFilter<T>, IWithInnerErrorFilter<T>
			where TException : Exception
		{
			return (arg, sp) => previous(arg, sp).IncludeError(func);
		}
	}
}
