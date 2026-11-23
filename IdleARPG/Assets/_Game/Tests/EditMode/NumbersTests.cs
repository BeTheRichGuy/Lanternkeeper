using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class NumbersTests
    {
        [TestCase(7.4f, "7")]
        [TestCase(9999f, "9999")]
        [TestCase(12345f, "1.2만")]
        [TestCase(456789f, "46만")]
        [TestCase(3_400_000f, "340만")]
        [TestCase(2.5e9f, "25억")]
        [TestCase(1.5e12f, "1.5조")]
        public void Big_numbers_stay_short(float v, string text) => Assert.That(DamageNumbers.Short(v), Is.EqualTo(text));
    }
}
