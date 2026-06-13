using NUnit.Framework;
using UDND.Core;
using UDND.Inventories;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class DropPolicySettingsTests
    {
        private DropPolicySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new DropPolicySettings();
        }

        // ---------- Defaults ----------

        [Test]
        public void Resolve_NullRequest_UsesDefaultResolverAndAllowPartial()
        {
            var resolved = _settings.Resolve(null, context: null);

            Assert.IsInstanceOf<FindAlternativeBlockedTargetResolver>(resolved.BlockedTargetResolver);
            Assert.IsTrue(resolved.AllowPartial);
            Assert.AreEqual(BatchMode.BestEffort, resolved.BatchMode);
            Assert.AreEqual(
                BlockedTargetResolutionKind.AlternativeSlots,
                resolved.BlockedTargetResolution);
            Assert.IsInstanceOf<MergeFirstPlacementCandidateOrderer>(
                resolved.AlternativeOrderer);
            Assert.AreEqual(PartialTransferMode.Allow, resolved.PartialTransferMode);
            Assert.IsTrue(resolved.AllowSameInventoryAlternativePlacement);
        }

        // ---------- Request overrides ----------

        [Test]
        public void Resolve_RequestResolver_TakesPrecedenceOverDefault()
        {
            var swap = new SwapBlockedTargetResolver();
            var request = DropRequestPolicy.WithResolver(swap);

            var resolved = _settings.Resolve(request, context: null);

            Assert.AreSame(swap, resolved.BlockedTargetResolver);
        }

        [Test]
        public void Resolve_RequestAllowPartial_TakesPrecedenceOverDefault()
        {
            var request = DropRequestPolicy.WithPartial(false);

            var resolved = _settings.Resolve(request, context: null);

            Assert.IsFalse(resolved.AllowPartial);
        }

        [Test]
        public void Resolve_RequestOnlyResolver_AllowPartialFromSettings()
        {
            // Request sets resolver but not AllowPartial — partial must fall back to default (true)
            var request = DropRequestPolicy.WithResolver(new SwapBlockedTargetResolver());

            var resolved = _settings.Resolve(request, context: null);

            Assert.IsTrue(resolved.AllowPartial);
            Assert.IsInstanceOf<SwapBlockedTargetResolver>(resolved.BlockedTargetResolver);
        }

        [Test]
        public void Resolve_RequestOnlyPartial_ResolverFromSettings()
        {
            var request = DropRequestPolicy.WithPartial(false);

            var resolved = _settings.Resolve(request, context: null);

            Assert.IsInstanceOf<FindAlternativeBlockedTargetResolver>(resolved.BlockedTargetResolver);
            Assert.IsFalse(resolved.AllowPartial);
        }

        [Test]
        public void Resolve_BatchMode_AlwaysFromSettings_NotInfluencedByRequest()
        {
            // DropRequestPolicy has no BatchMode field — it must always come from settings
            var request = DropRequestPolicy.WithResolver(new SwapBlockedTargetResolver());

            var resolved = _settings.Resolve(request, context: null);

            Assert.AreEqual(BatchMode.BestEffort, resolved.BatchMode);
        }
    }
}
