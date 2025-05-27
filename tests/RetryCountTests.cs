using NUnit.Framework;

namespace PoliNorError.Extensions.Http.Tests
{
	public class RetryCountTests
	{
		[Test]
		public void Should_Set_IsInfinite_To_True_When_Infinite_Created()
		{
			var retryCount = RetryCount.Infinite();

			Assert.That(retryCount.IsInfinite, Is.True);
		}

		[Test]
		public void Should_Set_Correct_Count_When_Positive_Value_Provided()
		{
			var retryCount = RetryCount.FromCount(5);

			Assert.That(retryCount.Count, Is.EqualTo(5));
			Assert.That(retryCount.IsInfinite, Is.False);
		}

		[Test]
		public void Should_Set_Default_Count_When_Zero_Provided()
		{
			var retryCount = RetryCount.FromCount(0);

			Assert.That(retryCount.Count, Is.EqualTo(1));
			Assert.That(retryCount.IsInfinite, Is.False);
		}

		[Test]
		public void Should_Set_Default_Count_When_Negative_Value_Provided()
		{
			var retryCount = RetryCount.FromCount(-10);

			Assert.That(retryCount.Count, Is.EqualTo(1));
			Assert.That(retryCount.IsInfinite, Is.False);
		}

		[Test]
		public void Should_Convert_MaxInt_To_RealInfiniteRetryCount()
		{
			var retryCount = RetryCount.FromCount(int.MaxValue);

			Assert.That(retryCount.Count, Is.EqualTo(int.MaxValue - 1));
			Assert.That(retryCount.IsInfinite, Is.False);
		}

		[Test]
		public void Should_Allow_Implicit_Conversion_From_Int()
		{
			RetryCount retryCount = 3;

			Assert.That(retryCount.Count, Is.EqualTo(3));
			Assert.That(retryCount.IsInfinite, Is.False);
		}
	}
}
