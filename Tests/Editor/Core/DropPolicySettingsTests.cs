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

        [Test]
        public void Resolve_NullRequest_UsesDefaultScalarPolicy()
        {
            var resolved = _settings.Resolve(null, context: null);

            Assert.AreEqual(
                BlockedTargetResolutionKind.FindAlternative,
                resolved.BlockedTargetResolution);
            Assert.IsInstanceOf<MergeFirstPlacementCandidateOrderer>(
                resolved.AlternativeOrderer);
            Assert.AreEqual(PartialTransferMode.Allow, resolved.PartialTransferMode);
            Assert.IsFalse(resolved.AllowSameInventoryAlternativePlacement);
            Assert.AreEqual(SwapDisplacementMode.SinglePlacement, resolved.SwapDisplacementMode);
        }


        [Test]
        public void Resolve_RequestOverridesSwapDisplacementMode()
        {
            var resolved = _settings.Resolve(
                DropRequestPolicy.WithSwap(SwapDisplacementMode.AllCoveredPlacements),
                context: null);

            Assert.AreEqual(BlockedTargetResolutionKind.Swap, resolved.BlockedTargetResolution);
            Assert.AreEqual(SwapDisplacementMode.AllCoveredPlacements, resolved.SwapDisplacementMode);
        }

        [Test]
        public void Resolve_RequestOverridesSelectedFields()
        {
            var orderer = new EmptyOnlyPlacementCandidateOrderer();
            var request = new DropRequestPolicy(
                BlockedTargetResolutionKind.FindAlternative,
                orderer,
                allowSameInventoryAlternativePlacement: false,
                PartialTransferMode.RequireFull);

            var resolved = _settings.Resolve(request, context: null);

            Assert.AreSame(orderer, resolved.AlternativeOrderer);
            Assert.IsFalse(resolved.AllowSameInventoryAlternativePlacement);
            Assert.AreEqual(
                PartialTransferMode.RequireFull,
                resolved.PartialTransferMode);
        }

        [Test]
        public void Resolve_RequestOnlyPartial_KeepsDefaultBlockedPolicy()
        {
            var resolved = _settings.Resolve(
                DropRequestPolicy.WithPartial(false),
                context: null);

            Assert.AreEqual(
                BlockedTargetResolutionKind.FindAlternative,
                resolved.BlockedTargetResolution);
            Assert.AreEqual(
                PartialTransferMode.RequireFull,
                resolved.PartialTransferMode);
        }
    }
}
