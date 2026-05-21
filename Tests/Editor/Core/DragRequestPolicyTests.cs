using NUnit.Framework;
using UDND.Core;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class DragRequestPolicyTests
    {
        [Test]
        public void Custom_StoresCustomAmount()
        {
            var policy = new DragRequestPolicy(DragAmount.Custom, customAmount: 7);

            Assert.AreEqual(DragAmount.Custom, policy.Amount);
            Assert.AreEqual(7, policy.CustomAmount);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Custom_WithNonPositiveAmount_ClampsToOne(int input)
        {
            var policy = new DragRequestPolicy(DragAmount.Custom, customAmount: input);

            Assert.AreEqual(1, policy.CustomAmount);
        }

        [TestCase(DragAmount.All)]
        [TestCase(DragAmount.One)]
        [TestCase(DragAmount.HalfUp)]
        [TestCase(DragAmount.HalfDown)]
        public void NonCustomAmount_IgnoresCustomAmount(DragAmount amount)
        {
            var policy = new DragRequestPolicy(amount, customAmount: 42);

            Assert.AreEqual(0, policy.CustomAmount,
                "CustomAmount must be zeroed for non-Custom drag modes");
        }
    }
}
