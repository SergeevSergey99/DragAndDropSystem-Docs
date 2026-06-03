using System.Collections.Generic;
using NUnit.Framework;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    [TestFixture]
    public class UniqueItemStrategyTests
    {
        private UniqueItemStrategy _strategy;
        private List<BaseSlot> _slots;
        private BaseSlot _prefab;

        [SetUp]
        public void SetUp()
        {
            _strategy = new UniqueItemStrategy();
        }

        [TearDown]
        public void TearDown()
        {
            TestSlotFactory.Dispose(_slots);
            TestSlotFactory.Dispose(_prefab);
            _slots = null;
            _prefab = null;
        }

        // ---------- TryAdd with explicit targetIndex ----------

        [Test]
        public void TryAdd_TargetEmpty_PlacesExactlyOneItem()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: 1);

            Assert.IsFalse(fullyPlaced, "Unique places 1 per call when addressed by index");
            Assert.IsFalse(_slots[1].IsEmpty);
            Assert.AreEqual(1, _slots[1].Stack.Count);
            Assert.AreEqual(1, stack.Count, "One item must remain in the source stack");
        }

        [Test]
        public void TryAdd_TargetOccupied_DoesNothing()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "existing"));
            var incoming = ItemStackBuilder.Unique(1, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, incoming, targetIndex: 0);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual("existing", _slots[0].Stack.ID);
            Assert.AreEqual(1, incoming.Count);
        }

        // ---------- TryAdd distribution across empty slots ----------

        [Test]
        public void TryAdd_NoTargetIndex_DistributesOnePerEmptySlot()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            foreach (var s in _slots)
                Assert.AreEqual(1, s.Stack.Count);
        }

        [Test]
        public void TryAdd_FiveItems_FourEmptySlots_LeavesOneInStack()
        {
            // Regression for the original user bug at the strategy layer:
            // Unique must place 4 items, leave 1 in the source stack, and not crash.
            _slots = TestSlotFactory.CreateSlots(4);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual(1, stack.Count, "One item must remain un-placed");
            foreach (var s in _slots)
                Assert.AreEqual(1, s.Stack.Count);
        }

        [Test]
        public void TryAdd_NoEmptySlots_ReturnsFalseAndLeavesStackIntact()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "b"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual(2, stack.Count);
        }

        // ---------- TryRemove ----------

        [Test]
        public void TryRemove_BySourceIndex_ClearsSlot()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            var adapter = new FakeItemAdapter("gem");
            _slots[1].SetStack(ItemStackBuilder.Of(adapter));

            bool removed = _strategy.TryRemove(_slots, adapter, count: 1, sourceIndex: 1);

            Assert.IsTrue(removed);
            Assert.IsTrue(_slots[1].IsEmpty);
        }

        [Test]
        public void TryRemove_WithoutIndex_ClearsFirstMatchingSlot()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "gem"));
            _slots[2].SetStack(ItemStackBuilder.Unique(1, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("gem"), count: 1, sourceIndex: -1);

            Assert.IsTrue(removed);
            Assert.IsTrue(_slots[1].IsEmpty);
            Assert.IsFalse(_slots[2].IsEmpty, "Only the first match should be cleared");
        }

        [Test]
        public void TryRemove_NoMatch_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("rock"), count: 1, sourceIndex: -1);

            Assert.IsFalse(removed);
            Assert.IsFalse(_slots[0].IsEmpty);
        }

        // ---------- TryAddToSlot ----------

        [Test]
        public void TryAddToSlot_EmptyTarget_PlacesOneItem()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], ensureFreeSlots: null, operationContext: new SlotOperationContext());

            Assert.IsTrue(placed);
            Assert.AreEqual(1, _slots[0].Stack.Count);
            Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void TryAddToSlot_OccupiedTarget_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "existing"));
            var stack = ItemStackBuilder.Unique(1, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], ensureFreeSlots: null, operationContext: null);

            Assert.IsFalse(placed);
            Assert.AreEqual("existing", _slots[0].Stack.ID);
            Assert.AreEqual(1, stack.Count);
        }

        // ---------- GetSlotCandidates ----------

        [Test]
        public void CanAcceptItem_HasEmptySlot_ReturnsTrueAndSuggestsIt()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.AreSame(_slots[1], selection.Slot as BaseSlot);
        }

        [Test]
        public void CanAcceptItem_AllFullAndNoDynamic_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "b"));
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsFalse(selection.Accepted);
            Assert.IsNull(selection.Slot);
        }

        [Test]
        public void CanAcceptItem_AllFullButDynamicPrefabAllowed_ReturnsTrue()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 2);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.IsTrue(selection.CreateNew, "Only dynamic capacity available — must be a forced-new selection");
        }

        // ---------- GetAcceptableCount ----------

        [Test]
        public void GetAcceptableCount_CountsEmptySlots_ClampedToDesired()
        {
            _slots = TestSlotFactory.CreateSlots(5);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            var request = MakeRequest("gem", 10);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(4, count);
        }

        [Test]
        public void GetAcceptableCount_ClampedToDesired_WhenCapacityExceedsRequest()
        {
            _slots = TestSlotFactory.CreateSlots(5);
            var request = MakeRequest("gem", 2);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void GetAcceptableCount_IncludesPotentialNewSlots()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 5);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);

            Assert.AreEqual(3, count);
        }

        // ---------- helpers ----------

        private static InventoryAcceptanceRequest MakeRequest(string itemId, int desiredCount)
            => new InventoryAcceptanceRequest(
                targetInventory: null,
                itemAdapter: new FakeItemAdapter(itemId),
                desiredCount: desiredCount);
    }
}
