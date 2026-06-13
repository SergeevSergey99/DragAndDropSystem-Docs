using NUnit.Framework;
using UDND.Core;
using UDND.Inventories;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class DropRequestPolicyTests
    {
        [Test]
        public void Constructor_StoresScalarFields()
        {
            var orderer = new EmptyOnlyPlacementCandidateOrderer();
            var policy = new DropRequestPolicy(
                BlockedTargetResolutionKind.AlternativeSlots,
                orderer,
                allowSameInventoryAlternativePlacement: false,
                PartialTransferMode.RequireFull);

            Assert.AreEqual(
                BlockedTargetResolutionKind.AlternativeSlots,
                policy.BlockedTargetResolution);
            Assert.AreSame(orderer, policy.AlternativeOrderer);
            Assert.AreEqual(false, policy.AllowSameInventoryAlternativePlacement);
            Assert.AreEqual(PartialTransferMode.RequireFull, policy.PartialTransferMode);
        }

        [Test]
        public void WithSwap_UsesSwapKind()
        {
            var policy = DropRequestPolicy.WithSwap();

            Assert.AreEqual(
                BlockedTargetResolutionKind.Swap,
                policy.BlockedTargetResolution);
        }

        [Test]
        public void WithAlternativeOrderer_StoresOrdererAndSameInventoryFlag()
        {
            var orderer = new EmptyFirstPlacementCandidateOrderer();
            var policy = DropRequestPolicy.WithAlternativeOrderer(
                orderer,
                allowSameInventoryAlternativePlacement: false);

            Assert.AreEqual(
                BlockedTargetResolutionKind.AlternativeSlots,
                policy.BlockedTargetResolution);
            Assert.AreSame(orderer, policy.AlternativeOrderer);
            Assert.AreEqual(false, policy.AllowSameInventoryAlternativePlacement);
        }

        [TestCase(true, PartialTransferMode.Allow)]
        [TestCase(false, PartialTransferMode.RequireFull)]
        public void WithPartial_SetsMode(
            bool allow,
            PartialTransferMode expected)
        {
            var policy = DropRequestPolicy.WithPartial(allow);

            Assert.AreEqual(expected, policy.PartialTransferMode);
            Assert.IsFalse(policy.BlockedTargetResolution.HasValue);
        }

        [Test]
        public void Merge_OverridingFieldsWin_AndUnsetFieldsKeepBaseValues()
        {
            var basePolicy = new DropRequestPolicy(
                BlockedTargetResolutionKind.AlternativeSlots,
                MergeFirstPlacementCandidateOrderer.Instance,
                allowSameInventoryAlternativePlacement: false,
                PartialTransferMode.RequireFull);
            var overridingPolicy = new DropRequestPolicy(
                BlockedTargetResolutionKind.Swap,
                partialTransferMode: PartialTransferMode.Allow);

            var merged = DropRequestPolicy.Merge(basePolicy, overridingPolicy).Value;

            Assert.AreEqual(BlockedTargetResolutionKind.Swap, merged.BlockedTargetResolution);
            Assert.AreSame(
                MergeFirstPlacementCandidateOrderer.Instance,
                merged.AlternativeOrderer);
            Assert.AreEqual(false, merged.AllowSameInventoryAlternativePlacement);
            Assert.AreEqual(PartialTransferMode.Allow, merged.PartialTransferMode);
        }

        [Test]
        public void Merge_BothNull_ReturnsNull()
        {
            Assert.IsFalse(DropRequestPolicy.Merge(null, null).HasValue);
        }
    }
}
