using NUnit.Framework;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    /// <summary>
    /// Tests for the JIT InventoryTransferService.TryTransferEntry (unified placement plan,
    /// stage 3): candidate-loop placement against real inventory state, per-entry rollback,
    /// partial transfer with the remainder staying in the source, and blocked-target policy.
    /// </summary>
    [TestFixture]
    public class InventoryTransferServiceTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;
        private readonly InventoryTransferService _service = new InventoryTransferService();

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;
        }

        private static ResolvedDropPolicy DefaultPolicy(
            PartialTransferMode partial = PartialTransferMode.Allow,
            BlockedTargetResolutionKind blocked = BlockedTargetResolutionKind.AlternativeSlots)
            => new ResolvedDropPolicy(
                blocked,
                new MergeFirstPlacementCandidateOrderer(),
                allowSameInventoryAlternativePlacement: true,
                partialTransferMode: partial);

        private EntryTransferResult Transfer(
            BaseSlot targetSlot = null,
            PartialTransferMode partial = PartialTransferMode.Allow,
            BlockedTargetResolutionKind blocked = BlockedTargetResolutionKind.AlternativeSlots)
        {
            var builder = DragContextBuilder.FromAllSlots(_source);
            var context = (targetSlot != null
                ? builder.ToTargetSlot(targetSlot, _target)
                : builder.ToTarget(_target)).Build();

            return _service.TryTransferEntry(new TransferEntryRequest(
                context,
                context.Entries[0],
                _target,
                targetSlot,
                DefaultPolicy(partial, blocked)));
        }

        private static int CountFilledSlots(UniversalInventory inventory)
        {
            int filled = 0;
            foreach (var slot in inventory.Slots)
                if (!slot.IsEmpty)
                    filled++;
            return filled;
        }

        [Test]
        public void AreaDrop_LargeUniqueStack_DistributesOnePerSlot_RemainderStaysInSource()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(6)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(10, "gem"));

            var result = Transfer();

            Assert.AreEqual(EntryTransferStatus.Partial, result.Status);
            Assert.AreEqual(10, result.RequestedAmount);
            Assert.AreEqual(6, result.TransferredAmount);
            Assert.AreEqual(4, result.RemainingAmount);
            Assert.AreEqual(6, result.Outcomes.Count, "Unique target must produce one create outcome per item");
            Assert.AreEqual(6, CountFilledSlots(_target));
            Assert.AreEqual(4, _source.GetSlot(0).Stack.Count, "Remainder must stay in the source slot");

            var report = new TransferExecutionReport(new[] { result });
            Assert.AreEqual(4, report.RequestedAmount - report.TransferredAmount);
        }

        [Test]
        public void AreaDrop_RequireFull_RollsBackWholeEntry()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(6)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(10, "gem"));

            var result = Transfer(partial: PartialTransferMode.RequireFull);

            Assert.AreEqual(EntryTransferStatus.Failed, result.Status);
            Assert.AreEqual(0, result.TransferredAmount);
            Assert.AreEqual(0, CountFilledSlots(_target), "RequireFull rollback must leave the target empty");
            Assert.AreEqual(10, _source.GetSlot(0).Stack.Count, "RequireFull rollback must restore the source");
        }

        [Test]
        public void AreaDrop_Separable_MergesIntoPartialStack_ThenSpills()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(5)
                .WithFixedSlots(2)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(5)
                .WithFixedSlots(2)
                .Build();

            _target.TryAddStack(ItemStackBuilder.Unique(3, "coin")); // 3/5 in target[0]
            _source.TryAddStack(ItemStackBuilder.Unique(4, "coin"));

            var result = Transfer();

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status);
            Assert.AreEqual(4, result.TransferredAmount);
            Assert.AreEqual(2, result.Outcomes.Count, "Expected a merge outcome and a spill create outcome");
            Assert.AreEqual(PlacementTransferOutcomeKind.Merge, result.Outcomes[0].Kind);
            Assert.AreEqual(2, result.Outcomes[0].Amount);
            Assert.AreEqual(PlacementTransferOutcomeKind.Create, result.Outcomes[1].Kind);
            Assert.AreEqual(2, result.Outcomes[1].Amount);
            Assert.AreEqual(5, _target.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, _target.GetSlot(1).Stack.Count);
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
        }

        [Test]
        public void ExplicitTarget_MergeUsesExactCapacity_RemainderGoesToAlternatives()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(5)
                .WithFixedSlots(2)
                .Build();

            _target.TryAddStack(ItemStackBuilder.Unique(4, "coin")); // 4/5 in target[0]
            _source.TryAddStack(ItemStackBuilder.Unique(3, "coin"));

            var result = Transfer(targetSlot: _target.GetSlot(0));

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status);
            Assert.AreEqual(3, result.TransferredAmount);
            Assert.AreEqual(5, _target.GetSlot(0).Stack.Count, "Explicit merge must fill the hinted slot first");
            Assert.AreEqual(2, _target.GetSlot(1).Stack.Count, "Remainder must spill via alternative candidates");
        }

        [Test]
        public void ExplicitTarget_Blocked_RejectPolicy_FailsWithoutMutation()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(1, "sword"));
            _target.TryAddStack(ItemStackBuilder.Unique(1, "shield")); // occupies target[0]

            var result = Transfer(targetSlot: _target.GetSlot(0), blocked: BlockedTargetResolutionKind.Reject);

            Assert.AreEqual(EntryTransferStatus.Failed, result.Status);
            Assert.AreEqual(0, result.TransferredAmount);
            Assert.AreEqual(1, _source.GetSlot(0).Stack.Count, "Reject must leave the source untouched");
            Assert.IsTrue(_target.GetSlot(1).IsEmpty, "Reject must not fall back to alternative slots");

            var dropResult = new TransferExecutionReport(new[] { result }).ToDropResult(_target);
            Assert.AreEqual("Target slot is blocked", dropResult.FailureReason);
        }

        [Test]
        public void ExplicitTarget_Blocked_AlternativePolicy_UsesOtherSlot()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(1, "sword"));
            _target.TryAddStack(ItemStackBuilder.Unique(1, "shield"));

            var result = Transfer(targetSlot: _target.GetSlot(0));

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status);
            Assert.AreEqual(1, result.TransferredAmount);
            Assert.IsFalse(_target.GetSlot(1).IsEmpty, "Item must land in the alternative empty slot");
            Assert.AreEqual("shield", _target.GetSlot(0).Stack.ID, "Hinted occupied slot must stay untouched");
        }

        [Test]
        public void Outcomes_CarryExactSubStacks_TotalsMatchTransferredAmount()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(6)
                .WithFixedSlots(2)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(10, "coin"));

            var result = Transfer();

            Assert.AreEqual(EntryTransferStatus.Succeeded, result.Status);
            Assert.AreEqual(10, result.TransferredAmount);

            int removedTotal = 0;
            int addedTotal = 0;
            foreach (var outcome in result.Outcomes)
            {
                removedTotal += outcome.SourceRemovedStack.Count;
                addedTotal += outcome.TransferredStack.Count;
                Assert.AreEqual(outcome.SourceRemovedStack.Count, outcome.TransferredStack.Count,
                    "Each outcome must carry a balanced remove/add sub-stack");
            }

            Assert.AreEqual(10, removedTotal);
            Assert.AreEqual(10, addedTotal);
        }

        [Test]
        public void NoCapacity_FailsWithoutMutation()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(1, "sword"));
            _target.TryAddStack(ItemStackBuilder.Unique(1, "shield"));

            var result = Transfer();

            Assert.AreEqual(EntryTransferStatus.Failed, result.Status);
            Assert.AreEqual(1, _source.GetSlot(0).Stack.Count);
            Assert.AreEqual("shield", _target.GetSlot(0).Stack.ID);
        }
    }
}
