namespace PoliNorError.Extensions.Http.Tests
{
	internal class PolicyOptionsTests
	{
		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_AllowSettingConfigurationHandler(bool withFallack)
		{
			bool wasCalled = false;

			PolicyOptions options;

			if (withFallack)
			{
				options = new FallbackPolicyOptions
				{
					ConfigurePolicyResultHandling = _ => wasCalled = true
				};
			}
			else
			{
				options = new FallbackPolicyOptions
				{
					ConfigurePolicyResultHandling = _ => wasCalled = true
				};
			}

			var mockHandlers = new HttpPolicyResultHandlers();
			options.ConfigurePolicyResultHandling(mockHandlers);

			Assert.That(wasCalled, Is.True,
				"Configuration handler should be invoked");
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_SetConfigureErrorProcessing(bool withFallack)
		{
			PolicyOptions options;
			var bp = new BulkErrorProcessor();

			IPolicyBase rp;

			if (!withFallack)
			{
				options = new PolicyOptions();
				rp = new RetryPolicy(1, bp);
			}
			else
			{
				options = new FallbackPolicyOptions();
				rp = new FallbackPolicy(bp).WithFallbackAction(() => { });
			}

			bool invoked = false;
			options.ConfigureErrorProcessing = (b) => b.WithErrorProcessorOf((_) => invoked = true);
			options.ConfigureErrorProcessing(bp);

			var result = rp.Handle(() => throw new InvalidOperationException());
			if (!withFallack)
			{
				Assert.That(result.IsFailed, Is.True);
			}
			Assert.That(invoked, Is.True);
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_SetConfigureErrorFilter(bool withFallack)
		{
			PolicyResult result;
			if (!withFallack)
			{
				var options = new PolicyOptions();
				options.ConfigureErrorFilter = (ef) => ef.ExcludeError<InvalidOperationException>();

				var rp = new RetryPolicy(1).AddErrorFilter(options.ConfigureErrorFilter);

				result = rp.Handle(() => throw new InvalidOperationException());
			}
			else
			{
				var options = new PolicyOptions();
				options.ConfigureErrorFilter = (ef) => ef.ExcludeError<InvalidOperationException>();

				var rp = new FallbackPolicy().WithFallbackAction(() => { }).AddErrorFilter(options.ConfigureErrorFilter);
				result = rp.Handle(() => throw new InvalidOperationException());
			}
			Assert.That(result.ErrorFilterUnsatisfied, Is.True);
		}

		[Test]
		[TestCase(true)]
		[TestCase(false)]
		public void Should_SetAndRetrievePolicyName(bool withFallack)
		{
			// Arrange
			var options = withFallack ? new FallbackPolicyOptions() : new PolicyOptions();

			// Act
			options.PolicyName = "MyCustomPolicy";

			// Assert
			Assert.That(options.PolicyName, Is.EqualTo("MyCustomPolicy"));
		}
	}
}
