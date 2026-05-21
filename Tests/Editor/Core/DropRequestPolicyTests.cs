using NUnit.Framework;
using UDND.Core;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class DropRequestPolicyTests
    {
        // ---------- Construction ----------

        [Test]
        public void Constructor_StoresFields()
        {
            var resolver = new FindAlternativeBlockedTargetResolver();
            var policy = new DropRequestPolicy(resolver, allowPartial: false);

            Assert.AreSame(resolver, policy.BlockedTargetResolver);
            Assert.AreEqual(false, policy.AllowPartial);
        }

        [Test]
        public void WithResolver_WrapsResolver_LeavesAllowPartialNull()
        {
            var resolver = new SwapBlockedTargetResolver();
            var policy = DropRequestPolicy.WithResolver(resolver);

            Assert.AreSame(resolver, policy.BlockedTargetResolver);
            Assert.IsFalse(policy.AllowPartial.HasValue);
        }

        [Test]
        public void WithSwap_UsesSwapResolver()
        {
            var policy = DropRequestPolicy.WithSwap();

            Assert.IsInstanceOf<SwapBlockedTargetResolver>(policy.BlockedTargetResolver);
        }

        [Test]
        public void WithFindAlternative_DefaultPlacementStrategy_UsesFindAlternativeResolver()
        {
            var policy = DropRequestPolicy.WithFindAlternative();

            var resolver = policy.BlockedTargetResolver as FindAlternativeBlockedTargetResolver;
            Assert.IsNotNull(resolver);
            Assert.IsNotNull(resolver.AlternativePlacementStrategy,
                "FindAlternative without explicit strategy must keep its default (MergeFirst)");
        }

        [Test]
        public void WithFindAlternative_WithCustomPlacement_OverridesStrategy()
        {
            var custom = new EmptyFirstAlternativePlacementStrategy();

            var policy = DropRequestPolicy.WithFindAlternative(custom);

            var resolver = (FindAlternativeBlockedTargetResolver)policy.BlockedTargetResolver;
            Assert.AreSame(custom, resolver.AlternativePlacementStrategy);
        }

        [Test]
        public void WithFindAlternative_CanDisableSameInventoryAlternativePlacement()
        {
            var policy = DropRequestPolicy.WithFindAlternative(
                allowSameInventoryAlternativePlacement: false);

            var resolver = (FindAlternativeBlockedTargetResolver)policy.BlockedTargetResolver;
            Assert.IsFalse(resolver.AllowSameInventoryAlternativePlacement);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WithPartial_SetsAllowPartial_LeavesResolverNull(bool allow)
        {
            var policy = DropRequestPolicy.WithPartial(allow);

            Assert.IsNull(policy.BlockedTargetResolver);
            Assert.AreEqual(allow, policy.AllowPartial);
        }

        // ---------- Merge ----------

        [Test]
        public void Merge_BothNull_ReturnsNull()
        {
            Assert.IsFalse(DropRequestPolicy.Merge(null, null).HasValue);
        }

        [Test]
        public void Merge_BaseNull_ReturnsOverriding()
        {
            var over = DropRequestPolicy.WithSwap();

            var merged = DropRequestPolicy.Merge(null, over);

            Assert.IsTrue(merged.HasValue);
            Assert.AreSame(over.BlockedTargetResolver, merged.Value.BlockedTargetResolver);
        }

        [Test]
        public void Merge_OverridingNull_ReturnsBase()
        {
            var baseP = DropRequestPolicy.WithPartial(false);

            var merged = DropRequestPolicy.Merge(baseP, null);

            Assert.IsTrue(merged.HasValue);
            Assert.AreEqual(false, merged.Value.AllowPartial);
        }

        [Test]
        public void Merge_OverridingResolver_TakesPrecedence()
        {
            var baseP = DropRequestPolicy.WithResolver(new FindAlternativeBlockedTargetResolver());
            var over = DropRequestPolicy.WithResolver(new SwapBlockedTargetResolver());

            var merged = DropRequestPolicy.Merge(baseP, over).Value;

            Assert.IsInstanceOf<SwapBlockedTargetResolver>(merged.BlockedTargetResolver);
        }

        [Test]
        public void Merge_OverridingResolverNull_KeepsBaseResolver()
        {
            var baseResolver = new SwapBlockedTargetResolver();
            var baseP = DropRequestPolicy.WithResolver(baseResolver);
            var over = DropRequestPolicy.WithPartial(true); // no resolver

            var merged = DropRequestPolicy.Merge(baseP, over).Value;

            Assert.AreSame(baseResolver, merged.BlockedTargetResolver);
            Assert.AreEqual(true, merged.AllowPartial);
        }

        [Test]
        public void Merge_OverridingAllowPartialNull_KeepsBaseAllowPartial()
        {
            var baseP = DropRequestPolicy.WithPartial(false);
            var over = DropRequestPolicy.WithResolver(new SwapBlockedTargetResolver());

            var merged = DropRequestPolicy.Merge(baseP, over).Value;

            Assert.AreEqual(false, merged.AllowPartial);
            Assert.IsInstanceOf<SwapBlockedTargetResolver>(merged.BlockedTargetResolver);
        }

        [Test]
        public void Merge_BothAllowPartialSet_OverridingWins()
        {
            var baseP = DropRequestPolicy.WithPartial(true);
            var over = DropRequestPolicy.WithPartial(false);

            var merged = DropRequestPolicy.Merge(baseP, over).Value;

            Assert.AreEqual(false, merged.AllowPartial);
        }
    }
}
