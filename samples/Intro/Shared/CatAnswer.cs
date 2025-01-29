using PoliNorError.Extensions.Http;
using System;
using System.Threading.Tasks;

namespace Shared
{
	public class CatAnswer
	{
		private CatAnswer() { }

		public CatAnswer(string answer)
		{
			Answer = answer;
		}
		
        public string Answer { get; }

		public Exception  Error { get; private init; }

		public static CatAnswer FromError(Exception exception)
		{
			return new CatAnswer() { Error = exception, 
										IsCanceled = exception switch
										{
											HttpPolicyResultException httpError => httpError.IsCanceled,
											TaskCanceledException canceledException => canceledException.CancellationToken.IsCancellationRequested,
											_ => null
										}
			} ;
		}

		public bool? IsCanceled { get; private set; }

		public bool IsError => Error != null;

		public bool IsOk => !IsError;

	}

}
