using NUnit.Framework;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class HttpResponseMessageToHandleByPolicyAdapterTests
	{
		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public async Task Should_Adapt_For_AllNonSuccessfulStatusCodes_Work(bool successStatusCode)
		{
			var statusCodesToHandle = HttpErrorFilter.HandleAllNonSuccessStatusCodes();
			var response = new HttpResponseMessage(successStatusCode ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound);
			if (successStatusCode)
			{
				var result = await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(response, statusCodesToHandle);
				Assert.That(result, Is.Not.Null);
			}
			else
			{
				_ = Assert.ThrowsAsync<FailedHttpResponseException>(async () => await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(response, statusCodesToHandle));
			}
		}

		[Test]
		public void Should_Adapt_For_StatusCodesCategory_When_Store_Contains_Status_Code_Throws_HttpRequestException()
		{
			var statusCodesToHandle = HttpErrorFilter.HandleStatusCodeCategory(StatusCodesCategory.Status1XX);
			var response = new HttpResponseMessage(System.Net.HttpStatusCode.Continue);
			_ = Assert.ThrowsAsync<FailedHttpResponseException>(async() => await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(response, statusCodesToHandle));
		}

		[Test]
		public async Task Should_Adapt_For_StatusCodesCategory_When_Store_Not_Contains_Status_Code_Does_Not_Throw()
		{
			var statusCodesToHandle = HttpErrorFilter.HandleStatusCodeCategory(StatusCodesCategory.Status1XX);
			var response = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
			var result = await HttpResponseMessageToHandleByPolicyAdapter.AdaptAsync(response, statusCodesToHandle);
			Assert.That(result, Is.Not.Null);
		}
	}
}
