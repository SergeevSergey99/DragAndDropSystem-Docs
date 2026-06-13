using System.Linq;
using NUnit.Framework;
using UDND.Core;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    /// <summary>
    /// Integration tests for InventoryDropProcessor — the public entry point of the
    /// JIT transfer pipeline. Each test wires two real UniversalInventory instances
    /// via InventoryBuilder, builds a DragContext with DragContextBuilder, and asserts
    /// against the resulting TransferExecutionSummary + final slot state.
    ///
    /// Covers the scenarios that used to regress silently before the snapshot fix:
    ///   - 5 Unique items dropped into a 4-slot area (BestEffort)
    ///   - AllowPartial=false early reject
    ///   - Swap via BlockedTargetResolutionKind.Swap
    ///   - FindAlternative with EmptyFirst / MergeFirst placement
    /// </summary>
    [TestFixture]
    public class InventoryDropProcessorTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;
        }

        // ---------- 5 -> 4 regression (the bug that started all this) ----------

        [Test]
        public void ProcessDrop_FiveUnique_IntoFourEmptySlots_BestEffort_SourceKeepsOne()
        {
            // Regression guard for the original bug: 5 Unique items dragged into a
            // 4-slot target with BestEffort must land 4 items in the target and leave
            // exactly 1 in the source (in the LAST source slot that failed to transfer).
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(5)
                .WithName("Source5")
                .Build();

            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(4)
                .WithName("Target4")
                .Build();

            for (int i = 0; i < 5; i++)
                Assert.IsTrue(_source.TryAddStack(ItemStackBuilder.Unique(1, $"gem_{i}")));

            var context = DragContextBuilder.FromAllSlots(_source)
                .ToTarget(_target)
                .Build();

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context);

            Assert.IsTrue(report.Success, $"Expected success (partial), got: {report.FailureReason}");
            Assert.IsTrue(report.IsPartial, "Operation must report partial");
            Assert.AreEqual(4, report.TransferredAmount);
            Assert.AreEqual(4, CountFilledSlots(_target));
            Assert.AreEqual(1, CountFilledSlots(_source),
                "Exactly one source slot must retain its item after partial transfer");
        }

        [Test]
        public void ProcessDrop_AllowPartialFalse_SingleEntryCannotFitFully_Rejected()
        {
            // RequireFull is a per-entry transaction rule. Here a 10-coin source stack
            // can transfer only 5, so the entry must roll back.
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(20)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(5)
                .WithFixedSlots(1)
                .Build();

            _source.TryAddStack(ItemStackBuilder.Unique(10, "coin"));

            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context, DropRequestPolicy.WithPartial(false));

            Assert.IsFalse(report.Success, "AllowPartial=false must reject when the entry can only fit partially");
            Assert.AreEqual(0, report.TransferredAmount);
            Assert.AreEqual(10, _source.GetSlot(0).Stack.Count, "Source must be untouched on full reject");
            Assert.IsTrue(_target.GetSlot(0).IsEmpty, "Target must remain empty on full reject");
        }

        // ---------- Stackable: partial merge + spill ----------

        [Test]
        public void ProcessDrop_Stackable_WithLimit_MergesIntoPartial_AndSpillsIntoEmpty()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(5)
                .WithFixedSlots(2)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(5)
                .WithFixedSlots(3)
                .Build();

            // target[0] = 3/5, target[1] empty, target[2] empty
            _target.TryAddStack(ItemStackBuilder.Unique(3, "coin"));
            // source has 6 coins (fills source[0]=5, source[1]=1)
            _source.TryAddStack(ItemStackBuilder.Unique(6, "coin"));

            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context);

            Assert.IsTrue(report.Success);
            Assert.AreEqual(6, report.TransferredAmount,
                "All 6 must fit: 2 merge into target[0], remaining 4 spill into empty slot");
            Assert.AreEqual(0, _source.Slots.Sum(s => s.Stack?.Count ?? 0),
                "Source must be fully drained");
            Assert.AreEqual(9, _target.Slots.Sum(s => s.Stack?.Count ?? 0));
        }

        // ---------- AllowPartial semantics ----------

        [Test]
        public void ProcessDrop_AllowPartialTrue_PartialFillSucceeds()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(3)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();

            for (int i = 0; i < 3; i++)
                _source.TryAddStack(ItemStackBuilder.Unique(1, $"gem_{i}"));

            var context = DragContextBuilder.FromAllSlots(_source).ToTarget(_target).Build();

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context, DropRequestPolicy.WithPartial(true));

            Assert.IsTrue(report.Success);
            Assert.IsTrue(report.IsPartial);
            Assert.AreEqual(1, report.TransferredAmount);
            Assert.AreEqual(1, CountFilledSlots(_target));
            Assert.AreEqual(2, CountFilledSlots(_source), "Two items must remain in source");
        }

        // ---------- Swap resolver ----------

        [Test]
        public void ProcessDrop_WithSwapResolver_SingleTargetSlotOccupied_SwapsContents()
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

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context, DropRequestPolicy.WithSwap());

            Assert.IsTrue(report.Success, $"Swap must succeed, got: {report.FailureReason}");
            Assert.AreEqual("sword", _target.GetSlot(0).Stack.ItemAdapter.ItemId);
            Assert.AreEqual("shield", _source.GetSlot(0).Stack.ItemAdapter.ItemId);
        }

        // ---------- FindAlternative: EmptyFirst vs MergeFirst ----------

        [Test]
        public void ProcessDrop_FindAlternative_EmptyFirst_OccupiedTarget_GoesToEmptyNotMerge()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new SeparableStacksStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(3)
                .Build();

            // target[0] = other type (blocks direct drop), target[1] = same type (merge candidate),
            // target[2] = empty (EmptyFirst should prefer this).
            _target.TryAddStack(ItemStackBuilder.Unique(1, "other"));
            _target.GetSlot(1).SetStack(ItemStackBuilder.Unique(2, "coin"));

            _source.TryAddStack(ItemStackBuilder.Unique(3, "coin"));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithAlternativeOrderer(
                    EmptyFirstPlacementCandidateOrderer.Instance));

            Assert.IsTrue(report.Success);
            Assert.AreEqual("other", _target.GetSlot(0).Stack.ItemAdapter.ItemId, "Blocked target must stay untouched");
            Assert.AreEqual(2, _target.GetSlot(1).Stack.Count, "EmptyFirst must not merge into the partial stack");
            Assert.AreEqual(3, _target.GetSlot(2).Stack.Count, "EmptyFirst must place into the empty slot");
        }

        [Test]
        public void ProcessDrop_FindAlternative_MergeFirst_OccupiedTarget_MergesIntoPartial()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithMaxStackSize(10)
                .WithFixedSlots(3)
                .Build();

            _target.TryAddStack(ItemStackBuilder.Unique(1, "other"));
            _target.GetSlot(1).SetStack(ItemStackBuilder.Unique(2, "coin"));

            _source.TryAddStack(ItemStackBuilder.Unique(3, "coin"));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithAlternativeOrderer(
                    MergeFirstPlacementCandidateOrderer.Instance));

            Assert.IsTrue(report.Success);
            Assert.AreEqual(5, _target.GetSlot(1).Stack.Count, "MergeFirst must merge into existing coin stack");
            Assert.IsTrue(_target.GetSlot(2).IsEmpty, "Empty slot must remain empty when merge is preferred");
        }

        [Test]
        public void ProcessDrop_FindAlternative_SameInventoryFallbackDisabled_OccupiedTargetDoesNotMoveToEmptySlot()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(3)
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sword"));
            _source.GetSlot(1).SetStack(ItemStackBuilder.Unique(1, "shield"));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_source.GetSlot(1), _source)
                .Build();

            var processor = new InventoryDropProcessor(_source.GetSlot(1), _source, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithAlternativeOrderer(
                    allowSameInventoryAlternativePlacement: false));

            Assert.IsFalse(report.Success, "Same-inventory blocked drop must be rejected instead of using an empty fallback slot");
            Assert.AreEqual("sword", _source.GetSlot(0).Stack.ItemAdapter.ItemId);
            Assert.AreEqual("shield", _source.GetSlot(1).Stack.ItemAdapter.ItemId);
            Assert.IsTrue(_source.GetSlot(2).IsEmpty, "Fallback slot must stay empty");
        }

        [Test]
        public void ProcessDrop_FindAlternative_SameInventoryFallbackEnabled_OccupiedTargetMovesToEmptySlot()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(3)
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sword"));
            _source.GetSlot(1).SetStack(ItemStackBuilder.Unique(1, "shield"));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_source.GetSlot(1), _source)
                .Build();

            var processor = new InventoryDropProcessor(_source.GetSlot(1), _source, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithAlternativeOrderer());

            Assert.IsTrue(report.Success, $"Expected fallback move, got: {report.FailureReason}");
            Assert.IsTrue(_source.GetSlot(0).IsEmpty, "Source slot must be emptied after the move");
            Assert.AreEqual("shield", _source.GetSlot(1).Stack.ItemAdapter.ItemId, "Blocked target must stay untouched");
            Assert.AreEqual("sword", _source.GetSlot(2).Stack.ItemAdapter.ItemId, "Item must move into the alternative slot");
        }

        [Test]
        public void ProcessDrop_FindAlternative_CrossInventoryFallbackDisabled_StillMovesToAlternativeSlot()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sword"));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "shield"));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithAlternativeOrderer(
                    allowSameInventoryAlternativePlacement: false));

            Assert.IsTrue(report.Success, $"Cross-inventory fallback must remain enabled, got: {report.FailureReason}");
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
            Assert.AreEqual("shield", _target.GetSlot(0).Stack.ItemAdapter.ItemId);
            Assert.AreEqual("sword", _target.GetSlot(1).Stack.ItemAdapter.ItemId);
        }

        [Test]
        public void ProcessDrop_OccupiedSlotHandler_WithFindAlternative_TakesPriorityOverAlternativeSlot()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sword"));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "container"));

            var binding = _target.gameObject.AddComponent<TestOccupiedSlotBinding>();
            binding.HandledSlot = _target.GetSlot(0);
            _target.Initialize(binding);

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithAlternativeOrderer());

            Assert.IsTrue(report.Success, $"Occupied handler must succeed, got: {report.FailureReason}");
            Assert.Greater(binding.CanHandleCalls, 0, "Probe/execution must ask the data binding");
            Assert.AreEqual(1, binding.ExecuteCalls, "JIT service must use the occupied-slot handler");
            Assert.IsTrue(_source.GetSlot(0).IsEmpty, "Source must be consumed by the occupied-slot handler");
            Assert.AreEqual("container", _target.GetSlot(0).Stack.ItemAdapter.ItemId, "Occupied target must stay untouched");
            Assert.IsTrue(_target.GetSlot(1).IsEmpty, "FindAlternative must not move the item into the free slot");
        }

        [Test]
        public void ProcessDrop_RejectResolver_OccupiedTargetRejectedAndUnchanged()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(1)
                .Build();
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "sword"));
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "shield"));

            var context = DragContextBuilder
                .FromSlots(_source, 0)
                .ToTargetSlot(_target.GetSlot(0), _target)
                .Build();

            var processor = new InventoryDropProcessor(_target.GetSlot(0), _target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(
                context,
                DropRequestPolicy.WithReject());

            Assert.IsFalse(report.Success, "Reject resolver must not search alternatives or swap");
            Assert.AreEqual("sword", _source.GetSlot(0).Stack.ItemAdapter.ItemId);
            Assert.AreEqual("shield", _target.GetSlot(0).Stack.ItemAdapter.ItemId);
            Assert.IsTrue(_target.GetSlot(1).IsEmpty);
        }

        // ---------- Degenerate inputs ----------

        [Test]
        public void ProcessDrop_NullContext_FailsGracefully()
        {
            _target = new InventoryBuilder().WithFixedSlots(1).Build();

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(null);

            Assert.IsFalse(report.Success);
            Assert.IsNotNull(report.FailureReason);
        }

        [Test]
        public void ProcessDrop_EmptySourceSlot_FailsGracefully()
        {
            // Build a DragContext with an empty source — our builder refuses, so we hand-craft.
            _source = new InventoryBuilder().WithFixedSlots(1).Build();
            _target = new InventoryBuilder().WithFixedSlots(1).Build();

            var emptyStack = ItemStack.Empty();
            var context = new DragContext(emptyStack, _source.GetSlot(0), _source);

            var processor = new InventoryDropProcessor(_target, new GlobalRuleValidator());
            var report = processor.ProcessDropWithReport(context);

            Assert.IsFalse(report.Success, "Empty stack cannot produce a valid transfer");
        }

        // ---------- helpers ----------

        private static int CountFilledSlots(IInventory inv)
        {
            int n = 0;
            for (int i = 0; i < inv.SlotCount; i++)
                if (!inv.GetSlot(i).IsEmpty) n++;
            return n;
        }

    }

    public sealed class TestOccupiedSlotBinding : InventoryDataBindingBase
    {
        public BaseSlot HandledSlot { get; set; }
        public int CanHandleCalls { get; private set; }
        public int ExecuteCalls { get; private set; }

        protected override void Awake() { }

        protected override bool CanHandleOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
        {
            CanHandleCalls++;
            return ReferenceEquals(occupiedBaseSlot, HandledSlot);
        }

        protected override bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
        {
            ExecuteCalls++;
            entry.SourceBaseSlot.Clear();
            return true;
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context) { }
        protected override void OnItemRemovedFromUI(InventoryItemEventContext context) { }
        protected override void OnReloadUI() { }
    }
}
