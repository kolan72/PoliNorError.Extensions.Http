using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class PolicyWithNotFilterableError: IPolicyBase, IWithErrorFilter<PolicyWithNotFilterableError>, IWithInnerErrorFilter<PolicyWithNotFilterableError>
	{
		private readonly PolicyWithNotFilterableErrorProcessor _simplePolicyProcessor;

		public PolicyWithNotFilterableError(Func<Exception> exceptionGenerator, Type exceptionType)
		{
			_simplePolicyProcessor = new PolicyWithNotFilterableErrorProcessor(exceptionGenerator, exceptionType);
		}

		public IPolicyProcessor PolicyProcessor => _simplePolicyProcessor;

		public string PolicyName => "PolicyWithNotFilterableError";

		public PolicyResult Handle(Action action, CancellationToken token = default) => _simplePolicyProcessor.Handle(action, token);

		public PolicyResult<T> Handle<T>(Func<T> func, CancellationToken token = default) => throw new NotImplementedException();
		public Task<PolicyResult> HandleAsync(Func<CancellationToken, Task> func, bool configureAwait = false, CancellationToken token = default) => throw new NotImplementedException();

		public async Task<PolicyResult<T>> HandleAsync<T>(Func<CancellationToken, Task<T>> func, bool configureAwait = false, CancellationToken token = default)
						=> await _simplePolicyProcessor.HandleAsync(func, configureAwait, token);

		public PolicyWithNotFilterableError ExcludeError<TException1>(Func<TException1, bool> func = default) where TException1 : Exception => this;
		public PolicyWithNotFilterableError ExcludeError(Expression<Func<Exception, bool>> expression) => this;

		public PolicyWithNotFilterableError IncludeError<TException1>(Func<TException1, bool> func = default) where TException1 : Exception => this;
		public PolicyWithNotFilterableError IncludeError(Expression<Func<Exception, bool>> expression) => this;
		public PolicyWithNotFilterableError IncludeInnerError<TInnerException>(Func<TInnerException, bool> predicate = null) where TInnerException : Exception => throw new NotImplementedException();
		public PolicyWithNotFilterableError ExcludeInnerError<TInnerException>(Func<TInnerException, bool> predicate = null) where TInnerException : Exception => throw new NotImplementedException();
	}

	internal class PolicyWithNotFilterableErrorProcessor: PolicyProcessor
	{
		private readonly Func<Exception> _exceptionGenerator;

		private readonly ISimplePolicyProcessor _simplePolicyProcessor;

		private readonly Type _exceptionType;

		public PolicyWithNotFilterableErrorProcessor(Func<Exception> exceptionGenerator, Type exceptionType)
		{
			_exceptionGenerator = exceptionGenerator;
			_simplePolicyProcessor = SimplePolicyProcessor.CreateDefault(_bulkErrorProcessor);
			_exceptionType = exceptionType;
		}

		public PolicyResult Handle(Action _, CancellationToken token = default)
		{
			return _simplePolicyProcessor.ExcludeError<Exception>().Execute(() => _exceptionGenerator(), token);
		}

		public async Task<PolicyResult<T>> HandleAsync<T>(Func<CancellationToken, Task<T>> func, bool configureAwait = false, CancellationToken token = default)
		{
			return await _simplePolicyProcessor.ExcludeError(ex => ex.GetType() == _exceptionType).ExecuteAsync<T>(async(ct) => {
				T result = default;
				try
				{
					result = await func(ct).ConfigureAwait(configureAwait);
				}
				catch (Exception)
				{
					_exceptionGenerator();
				}
				_exceptionGenerator();
				return default;
			}, token) ;
		}
	}
}
