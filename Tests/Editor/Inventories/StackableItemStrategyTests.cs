using System.Collections.Generic;
using NUnit.Framework;
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
        public void TryAdd_NoTarget_FillsExistingStacksFirstThenEmptySlots()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "gem")); // room for 3 more
            _slots[1].SetStack(ItemStackBuilder.Unique(3, "rock"));
            // _slots[2] empty
            var stack = ItemStackBuilder.Unique(6, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(4, _slots[0].Stack.Count, "Existing gem stack filled to max first");
            Assert.AreEqual("rock", _slots[1].Stack.ID, "Non-matching stack untouched");
            Assert.AreEqual(3, _slots[2].Stack.Count, "Remainder spills into empty slot");
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
        public void TryAddToSlot_EmptyTarget_UsesRequestedSlot()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[1], null, new SlotOperationContext());

            Assert.IsTrue(placed);
            Assert.AreEqual(4, _slots[0].Stack.Count, "Existing partial stack must stay untouched");
            Assert.AreEqual(3, _slots[1].Stack.Count, "Explicit empty target receives the new stack");
        }

        // ---------- CanAcceptItem ----------

        [Test]
        public void CanAcceptItem_PartialSlotWithRoom_SuggestsIt()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 2);

            bool can = _strategy.CanAcceptItem(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null, out var suggested);

            Assert.IsTrue(can);
            Assert.AreSame(_slots[0], suggested);
        }

        [Test]
        public void CanAcceptItem_AllFullAtLimit_ReturnsFalse()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 1);

            bool can = _strategy.CanAcceptItem(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null, out var suggested);

            Assert.IsFalse(can);
            Assert.IsNull(suggested);
        }

        [Test]
        public void CanAcceptItem_OnlyDifferentItems_SuggestsEmptySlot()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "rock"));
            var request = MakeRequest("gem", 1);

            bool can = _strategy.CanAcceptItem(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null, out var suggested);

            Assert.IsTrue(can);
            Assert.AreSame(_slots[1], suggested);
        }

        [Test]
        public void CanAcceptItem_FullAndNoEmpty_DynamicPrefabAllowed_ReturnsTrue()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock"));
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 1);

            bool can = _strategy.CanAcceptItem(_slots, request, canCreateNewSlot: true, potentialNewSlots: 2, baseSlotPrefab: _prefab, out var suggested);

            Assert.IsTrue(can);
            Assert.IsNull(suggested);
        }

        // ---------- GetAcceptableCount ----------

        [Test]
        public void GetAcceptableCount_SumsPartialAndEmptyCapacity_ClampedToDesired()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(7, "gem"));  // room for 3
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "rock")); // not counted
            // _slots[2] empty — room for 10
            var request = MakeRequest("gem", 20);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(13, count);
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
        public void GetAcceptableCount_IncludesPotentialNewSlots()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem")); // room for 1
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 10);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);

            // 1 (partial) + 3 new slots * 4 max = 13, clamped to desired 10
            Assert.AreEqual(10, count);
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
    }
}
