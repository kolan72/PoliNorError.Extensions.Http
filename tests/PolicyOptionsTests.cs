using NUnit.Framework;
using System;
using System.Linq;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class PolicyOptionsTests
	{
		[Test]
		public void Should_AllowSettingConfigurationHandler()
		{
			bool wasCalled = false;
			var options = new PolicyOptions
			{
				ConfigurePolicyResultHandling = _ => wasCalled = true
			};

			var mockHandlers = new HttpPolicyResultHandlers();
			options.ConfigurePolicyResultHandling(mockHandlers);

			Assert.That(wasCalled, Is.True,
				"Configuration handler should be invoked");
		}

		[Test]
		public void Should_SetConfigureErrorProcessing()
		{
			var options = new PolicyOptions();
			bool invoked = false;
			options.ConfigureErrorProcessing = (b) => b.WithErrorProcessorOf((_) => invoked = true);

			var bp = new BulkErrorProcessor();
			options.ConfigureErrorProcessing(bp);

			var rp = new RetryPolicy(1, bp);
			var result = rp.Handle(() => throw new InvalidOperationException());
			Assert.That(result.IsFailed, Is.True);
			Assert.That(result.Errors.Count, Is.EqualTo(2));
			Assert.That(invoked, Is.True);
		}

	}
}
