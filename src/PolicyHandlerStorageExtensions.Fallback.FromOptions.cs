using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http
{
	public static partial class PolicyHandlerStorageExtensions
	{
		internal static TStorage AddFallbackHandler<TStorage>(
			this IPolicyHandlerStorage<TStorage> storage,
			Func<CancellationToken, Task<HttpResponseMessage>> func,
			FallbackPolicyOptions options) where TStorage : IPolicyHandlerStorage<TStorage>
		{
			if (options is null)
				throw new ArgumentNullException(nameof(options));

			var bep = new BulkErrorProcessor();
			if (!(options.ConfigureErrorProcessing is null))
			{
				options.ConfigureErrorProcessing(bep);
			}

			var res = new FallbackPolicy(bep).WithAsyncFallbackFunc(func);

			if (!(options.ConfigurePolicyResultHandling is null))
			{
				var handlers = new HttpPolicyResultHandlers();
				options.ConfigurePolicyResultHandling(handlers);
				handlers.AttachTo(res);
			}

			if (!(options.ConfigureErrorFilter is null))
			{
				res.AddErrorFilter(options.ConfigureErrorFilter);
			}

			if (!(options.PolicyName is null))
			{
				res.WithPolicyName(options.PolicyName);
			}
			return storage.AddPolicyHandler(res);
		}
	}
}
