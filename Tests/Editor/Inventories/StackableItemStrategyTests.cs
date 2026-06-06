using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    [TestFixture]
    public class StackableItemStrategyTests
    {
        private StackableItemStrategy _strategy;
        private List<BaseSlot> _slots;
        private BaseSlot _prefab;

        [SetUp]
        public void SetUp()
        {
            _strategy = new StackableItemStrategy();
        }

        [TearDown]
        public void TearDown()
        {
            TestSlotFactory.Dispose(_slots);
            TestSlotFactory.Dispose(_prefab);
            _slots = null;
            _prefab = null;
        }

        // ---------- TryAdd with limit ----------

        [Test]
        public void TryAdd_EmptyTarget_ClampsToMaxStackSize()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool placed = _strategy.TryAdd(_slots, stack, targetIndex: 0);

            Assert.IsFalse(placed);
            Assert.AreEqual(3, _slots[0].Stack.Count);
            Assert.AreEqual(2, stack.Count, "Overflow stays in source when targetIndex is explicit");
            Assert.IsTrue(_slots[1].IsEmpty, "Explicit-target TryAdd must not spill into other slots");
        }

        [Test]
        public void TryAdd_OccupiedTargetSameType_MergesUpToLimit()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var stack = ItemStackBuilder.Unique(5, "gem");

            _strategy.TryAdd(_slots, stack, targetIndex: 0);

            Assert.AreEqual(5, _slots[0].Stack.Count);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void TryAdd_OccupiedTargetDifferentType_NoOp()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = _strategy.TryAdd(_slots, stack, targetIndex: 0);

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", _slots[0].Stack.ID);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void TryAdd_NoTarget_ItemPresent_FillsOnlyThatSlot_NoSpill()
        {
            // one-per-ID: an existing stack is topped up, but the overflow must NOT open a second stack.
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "gem")); // room for 3 more
            _slots[1].SetStack(ItemStackBuilder.Unique(3, "rock"));
            // _slots[2] empty
            var stack = ItemStackBuilder.Unique(6, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsFalse(fullyPlaced, "Overflow cannot open a second gem stack under one-per-ID");
            Assert.AreEqual(4, _slots[0].Stack.Count, "Existing gem stack filled to max");
            Assert.AreEqual("rock", _slots[1].Stack.ID, "Non-matching stack untouched");
            Assert.IsTrue(_slots[2].IsEmpty, "No spill into a second slot for the same item");
            Assert.AreEqual(3, stack.Count, "Overflow stays in the source stack");
        }

        [Test]
        public void TryAdd_NoTarget_ItemAbsent_PlacesIntoSingleEmptySlot()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "rock"));
            // _slots[1], _slots[2] empty
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(3, _slots[1].Stack.Count, "Absent item placed into first empty slot");
            Assert.IsTrue(_slots[2].IsEmpty, "Only one empty slot used");
        }

        [Test]
        public void TryAdd_NoTarget_Unlimited_PutsEverythingIntoOneSlot()
        {
            // Default _maxStackSize = 0 → unlimited
            _slots = TestSlotFactory.CreateSlots(3);
            var stack = ItemStackBuilder.Unique(50, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsTrue(fullyPlaced);
            Assert.AreEqual(50, _slots[0].Stack.Count);
            Assert.IsTrue(_slots[1].IsEmpty);
            Assert.IsTrue(_slots[2].IsEmpty);
        }

        [Test]
        public void TryAdd_NoRoomAnywhere_LeavesStackIntact()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock"));
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "rock"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual(3, stack.Count);
        }

        // ---------- TryRemove ----------

        [Test]
        public void TryRemove_BySourceIndex_RemovesCountFromThatSlot()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(5, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("gem"), count: 3, sourceIndex: 0);

            Assert.IsTrue(removed);
            Assert.AreEqual(2, _slots[0].Stack.Count);
        }

        [Test]
        public void TryRemove_BySourceIndex_WrongType_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(5, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("rock"), count: 1, sourceIndex: 0);

            Assert.IsFalse(removed);
            Assert.AreEqual(5, _slots[0].Stack.Count);
        }

        [Test]
        public void TryRemove_WithoutIndex_DistributesAcrossMatchingSlots()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem"));
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "rock"));
            _slots[2].SetStack(ItemStackBuilder.Unique(4, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("gem"), count: 5, sourceIndex: -1);

            Assert.IsTrue(removed);
            Assert.IsTrue(_slots[0].IsEmpty, "First gem slot fully drained");
            Assert.AreEqual("rock", _slots[1].Stack.ID, "Non-matching slot untouched");
            Assert.AreEqual(2, _slots[2].Stack.Count, "Remaining 2 removed from second gem slot");
        }

        [Test]
        public void TryRemove_NoMatch_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("rock"), count: 1, sourceIndex: -1);

            Assert.IsFalse(removed);
        }

        [Test]
        public void Query_ShapedPlacement_CountsCoveredCellsOnce()
        {
            var inventory = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, placement.CoveredIndices);

                var slots = new List<BaseSlot>(inventory.Slots);

                var sameShapeItem = new ShapeAdapter("bag", 2, 2);
                Assert.IsTrue(_strategy.Contains(slots, sameShapeItem));
                Assert.AreEqual(1, _strategy.GetItemCount(slots, sameShapeItem));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void GetSlotCandidates_ShapedPlacementWithRoom_ReportsRemainingCapacity()
        {
            // C2 (ShapedStacking-Plan.md): a shaped item is no longer capped at 1. An existing shaped
            // placement is a stackable one-per-ID location with capacity = maxStack - count; no second
            // placement (and no new slot) is offered for an item that already exists.
            var inventory = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                _strategy.SetMaxStackSize(5, allowItemOverride: false);
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));
                CollectionAssert.AreEqual(new[] { 0, 1 }, placement.CoveredIndices);

                var request = new InventoryAcceptanceRequest(inventory, new ShapeAdapter("bag", 2, 1), 3);
                var candidates = _strategy.GetSlotCandidates(
                    inventory.Slots, request, canCreateNewSlot: true, potentialNewSlots: 2, baseSlotPrefab: null);

                Assert.IsFalse(candidates.CanCreateNewSlot, "one-per-ID: no second placement for an existing item");
                Assert.AreEqual(1, candidates.Slots.Count, "Only the existing placement's anchor is eligible");
                Assert.AreEqual(0, candidates.Slots[0].Slot.Index, "Candidate is the placement anchor");
                Assert.AreEqual(4, candidates.Slots[0].RemainingCapacity, "maxStack(5) - count(1)");
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        // ---------- TryAddToSlot ----------

        [Test]
        public void TryAddToSlot_EmptyTarget_PlacesUpToMaxStackSize()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], null, new SlotOperationContext());

            Assert.IsTrue(placed);
            Assert.AreEqual(3, _slots[0].Stack.Count);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void TryAddToSlot_OccupiedSameType_Merges()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], null, new SlotOperationContext());

            Assert.IsTrue(placed);
            Assert.AreEqual(7, _slots[0].Stack.Count);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void TryAddToSlot_OccupiedDifferentType_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(1, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], null, null);

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", _slots[0].Stack.ID);
        }

        [Test]
        public void TryAddToSlot_EmptyTarget_ItemExistsElsewhere_RejectedByOnePerId()
        {
            // one-per-ID: cannot open a second gem stack in an empty slot while gem already exists.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[1], null, new SlotOperationContext());

            Assert.IsFalse(placed, "Second stack of the same item is forbidden under one-per-ID");
            Assert.AreEqual(4, _slots[0].Stack.Count, "Existing partial stack untouched");
            Assert.IsTrue(_slots[1].IsEmpty, "Empty target left empty");
            Assert.AreEqual(3, stack.Count, "Stack stays in source");
        }

        // ---------- GetSlotCandidates ----------

        [Test]
        public void CanAcceptItem_PartialSlotWithRoom_SuggestsIt()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 2);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.AreSame(_slots[0], selection.Slot as BaseSlot);
        }

        [Test]
        public void CanAcceptItem_AllFullAtLimit_ReturnsFalse()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsFalse(selection.Accepted);
            Assert.IsNull(selection.Slot);
        }

        [Test]
        public void CanAcceptItem_ItemPresentButFull_WithEmptySlot_RejectsSecondStack()
        {
            // one-per-ID: a full existing stack must NOT spill into an empty slot.
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem")); // full
            // _slots[1] empty
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: true, potentialNewSlots: 2, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsFalse(candidates.HasAny, "Full existing stack + one-per-ID => no candidates, no new slot");
            Assert.IsFalse(selection.Accepted);
        }

        [Test]
        public void CanAcceptItem_OnlyDifferentItems_SuggestsEmptySlot()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "rock"));
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.AreSame(_slots[1], selection.Slot as BaseSlot);
        }

        [Test]
        public void CanAcceptItem_FullAndNoEmpty_DynamicPrefabAllowed_ReturnsTrue()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock"));
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: true, potentialNewSlots: 2, baseSlotPrefab: _prefab);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.IsTrue(selection.CreateNew, "Only dynamic capacity available — must be a forced-new selection");
        }

        [Test]
        public void GetSlotCandidates_SameInventorySourcePlacement_ExcludesAllCoveredCells()
        {
            var inventory = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));
                CollectionAssert.AreEqual(new[] { 0, 1 }, placement.CoveredIndices);

                var sourceSlot = inventory.GetSlot(1);
                var entry = new DragEntry(stack.CreateCopy(), sourceSlot, inventory, placement);
                var context = new DragContext(new[] { entry });
                var request = new InventoryAcceptanceRequest(
                    inventory,
                    stack.PrimaryAdapter,
                    1,
                    context,
                    entry);

                var candidates = _strategy.GetSlotCandidates(
                    inventory.Slots,
                    request,
                    canCreateNewSlot: false,
                    potentialNewSlots: 0,
                    baseSlotPrefab: null);

                Assert.IsTrue(candidates.HasAny, "The source placement must not make the item look already present.");
                foreach (var candidate in candidates.Slots)
                {
                    Assert.AreNotEqual(0, candidate.Slot.Index);
                    Assert.AreNotEqual(1, candidate.Slot.Index);
                }
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        // ---------- GetAcceptableCount ----------

        [Test]
        public void GetAcceptableCount_ItemPresent_OnlyThatSlotRemainder()
        {
            // one-per-ID: capacity is the remainder of the single existing stack, no empty-slot sum.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(7, "gem"));  // room for 3
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "rock")); // not counted
            // _slots[2] empty — must NOT be counted under one-per-ID
            var request = MakeRequest("gem", 20);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(3, count, "Only the existing gem slot's remaining capacity counts");
        }

        [Test]
        public void GetAcceptableCount_ItemAbsent_SingleEmptySlotCapacity()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock"));
            // _slots[1], _slots[2] empty
            var request = MakeRequest("gem", 20);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(10, count, "Absent item gets one empty slot's worth of capacity");
        }

        [Test]
        public void GetAcceptableCount_EarlyExits_WhenCapacityReachesDesired()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            var request = MakeRequest("gem", 5);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(5, count);
        }

        [Test]
        public void GetAcceptableCount_ItemPresent_NewSlotsNotCounted()
        {
            // one-per-ID: an existing stack disables new-slot capacity for the same item.
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem")); // room for 1
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 10);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);

            Assert.AreEqual(1, count, "Only the existing stack's remainder; no second stack via new slots");
        }

        [Test]
        public void GetAcceptableCount_ItemAbsentNoEmpty_NewSlotCapacityCounted()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock")); // no empty, item absent
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 10);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);

            Assert.AreEqual(4, count, "Absent item with no empty slot uses one new slot's capacity");
        }

        [Test]
        public void GetAcceptableCount_ItemLimitOverride_Honored()
        {
            _strategy.SetMaxStackSize(99, allowItemOverride: true);
            _slots = TestSlotFactory.CreateSlots(1);
            var request = new InventoryAcceptanceRequest(
                targetInventory: null,
                itemAdapter: new LimitedFakeAdapter("gem", maxStackSize: 3),
                desiredCount: 10);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(3, count, "Per-item IStackSizeLimitable overrides strategy default");
        }

        // ---------- helpers ----------

        private static InventoryAcceptanceRequest MakeRequest(string itemId, int desiredCount)
            => new InventoryAcceptanceRequest(
                targetInventory: null,
                itemAdapter: new FakeItemAdapter(itemId),
                desiredCount: desiredCount);

        private sealed class LimitedFakeAdapter : IItemAdapter, IStackSizeLimitable
        {
            public string ItemId { get; }
            public string DisplayName { get; }
            public UnityEngine.Sprite Icon => null;
            public int MaxStackSize { get; }

            public LimitedFakeAdapter(string itemId, int maxStackSize)
            {
                ItemId = itemId;
                DisplayName = itemId;
                MaxStackSize = maxStackSize;
            }
        }

        private sealed class ShapeAdapter : IItemAdapter, IItemPlacementShapeProvider
        {
            public ShapeAdapter(string itemId, int width, int height)
            {
                ItemId = itemId;
                DisplayName = itemId;
                PlacementShape = new RectPlacementShape(width, height);
            }

            public string ItemId { get; }
            public string DisplayName { get; }
            public Sprite Icon => null;
            public IPlacementShape PlacementShape { get; }
        }
    }
}
