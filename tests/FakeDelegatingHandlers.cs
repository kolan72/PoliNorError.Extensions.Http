using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	public class DelegatingHandlerThatThrowsNotHttpException : DelegatingHandler
	{
		private readonly ErrorType _errorType;

		public DelegatingHandlerThatThrowsNotHttpException(ErrorType errorType)
		{
			_errorType = errorType;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await Task.Delay(1);
			switch (_errorType)
			{
				case ErrorType.Argument:
					throw new ArgumentException("Test");
				case ErrorType.InvalidOperation:
					throw new InvalidOperationException();
				default:
					throw new NotImplementedException();
			}
		}

		public enum ErrorType
		{
			InvalidOperation,
			Argument
		}
	}

	public class DelegatingHandlerThatReturnsBadStatusCode : DelegatingHandler
	{
		private readonly Func<int, Task<HttpResponseMessage>> _responseFactory;
		public int Attempts { get; private set; }

		public DelegatingHandlerThatReturnsBadStatusCode(HttpStatusCode httpStatusCode) :this(_ => Task.FromResult(new HttpResponseMessage(httpStatusCode)))
		{
		}

		public DelegatingHandlerThatReturnsBadStatusCode(Func<int, Task<HttpResponseMessage>> responseFactory)
		{
			_responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
		}

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return await _responseFactory.Invoke(++Attempts);
		}
	}
}
