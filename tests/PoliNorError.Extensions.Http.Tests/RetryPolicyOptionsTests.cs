using NUnit.Framework;
using System;

namespace PoliNorError.Extensions.Http.Tests
{
	internal class RetryPolicyOptionsTests
	{
		[Test]
		public void Should_Set_And_Get_RetryDelay()
		{
			// Arrange
			var options = new RetryPolicyOptions();
			var expectedDelay = ConstantRetryDelay.Create(TimeSpan.FromTicks(1));

			// Act
			options.RetryDelay = expectedDelay;

			// Assert
			Assert.That(options.RetryDelay, Is.EqualTo(expectedDelay));
		}

		[Test]
		public void Should_Set_And_Get_ProcessRetryAfterHeader()
		{
			var options = new RetryPolicyOptions();

			options.ProcessRetryAfterHeader = true;

			Assert.That(options.ProcessRetryAfterHeader, Is.True);
		}
	}
}
