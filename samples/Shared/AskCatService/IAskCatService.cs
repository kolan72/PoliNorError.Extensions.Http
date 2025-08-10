using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
	public interface IAskCatService
	{
		Task<CatAnswer> GetCatFactAsync(CancellationToken token = default);
	}
}
