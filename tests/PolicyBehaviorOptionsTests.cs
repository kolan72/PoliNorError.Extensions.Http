using NUnit.Framework;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class PolicyBehaviorOptionsTests
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
	}
}
