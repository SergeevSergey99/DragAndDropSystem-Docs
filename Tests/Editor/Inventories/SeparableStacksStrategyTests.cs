using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests.Inventories
{
    [TestFixture]
    public class SeparableStacksStrategyTests
    {
        private SeparableStacksStrategy _strategy;
        private List<BaseSlot> _slots;
        private BaseSlot _prefab;

        [SetUp]
        public void SetUp()
        {
            _strategy = new SeparableStacksStrategy();
        }

        [TearDown]
        public void TearDown()
        {
            TestSlotFactory.Dispose(_slots);
            TestSlotFactory.Dispose(_prefab);
            _slots = null;
            _prefab = null;
        }

        // ---------- TryAdd with target ----------

        [Test]
        public void TryAdd_EmptyTarget_ClampsToMaxStackSize()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool placed = _strategy.TryAdd(_slots, stack, targetIndex: 0);

            Assert.IsFalse(placed);
            Assert.AreEqual(3, _slots[0].Stack.Count);
            Assert.AreEqual(2, stack.Count);
            Assert.IsTrue(_slots[1].IsEmpty, "Explicit target must not spill into other slots");
        }

        [Test]
        public void TryAdd_OccupiedTargetSameType_MergesWhenAllowed()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem"));
            var stack = ItemStackBuilder.Unique(4, "gem");

            _strategy.TryAdd(_slots, stack, targetIndex: 0);

            Assert.AreEqual(7, _slots[0].Stack.Count);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void TryAdd_OccupiedTargetDifferentType_Rejects()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = _strategy.TryAdd(_slots, stack, targetIndex: 0);

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", _slots[0].Stack.ID);
            Assert.AreEqual(2, stack.Count);
        }

        // ---------- TryAdd programmatic (no target) ----------

        [Test]
        public void TryAdd_NoTarget_FillsExistingStackFirst_ThenEmptySlot()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem")); // room for 2
            // _slots[1] empty
            // _slots[2] empty
            var stack = ItemStackBuilder.Unique(8, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(5, _slots[0].Stack.Count);
            Assert.AreEqual(5, _slots[1].Stack.Count);
            Assert.AreEqual(1, _slots[2].Stack.Count);
        }

        [Test]
        public void TryAdd_NoTarget_SpillsAcrossEmptySlots_UntilStackSatisfied()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool fullyPlaced = _strategy.TryAdd(_slots, stack, targetIndex: -1);

            Assert.IsTrue(fullyPlaced);
            Assert.AreEqual(2, _slots[0].Stack.Count);
            Assert.AreEqual(2, _slots[1].Stack.Count);
            Assert.AreEqual(1, _slots[2].Stack.Count);
        }

        // ---------- TryRemove ----------

        [Test]
        public void TryRemove_BySourceIndex_Removes()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(4, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("gem"), count: 2, sourceIndex: 0);

            Assert.IsTrue(removed);
            Assert.AreEqual(2, _slots[0].Stack.Count);
        }

        [Test]
        public void TryRemove_WithoutIndex_DrainsAcrossSeparateStacks()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            _slots[1].SetStack(ItemStackBuilder.Unique(3, "gem"));

            bool removed = _strategy.TryRemove(_slots, new FakeItemAdapter("gem"), count: 4, sourceIndex: -1);

            Assert.IsTrue(removed);
            Assert.IsTrue(_slots[0].IsEmpty);
            Assert.AreEqual(1, _slots[1].Stack.Count);
        }

        // ---------- TryAddToSlot — KEY DIFFERENCE from Stackable ----------

        [Test]
        public void TryAddToSlot_EmptyTarget_DoesNotRedirectToExistingPartialStack()
        {
            // Core "separable" guarantee: drop on empty slot creates a NEW pile,
            // even if another slot has room for the same item. Two stacks coexist.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(4, "gem"));
            // _slots[1] empty — the drop target
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[1], null, new SlotOperationContext());

            Assert.IsTrue(placed);
            Assert.AreEqual(4, _slots[0].Stack.Count, "Existing stack must remain untouched");
            Assert.AreEqual(3, _slots[1].Stack.Count, "New separate pile created");
        }

        [Test]
        public void TryAddToSlot_EmptyTarget_ClampsToMaxStackSize()
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
        }

        [Test]
        public void TryAddToSlot_OccupiedDifferentType_Rejects()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(1, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], null, null);

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", _slots[0].Stack.ID);
        }

        [Test]
        public void TryAddToSlot_OccupiedAtCapacity_Rejects()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = _strategy.TryAddToSlot(_slots, stack, _slots[0], null, null);

            Assert.IsFalse(placed);
            Assert.AreEqual(3, _slots[0].Stack.Count);
            Assert.AreEqual(2, stack.Count);
        }

        // ---------- TryGetCandidate ----------

        [Test]
        public void TryGetCandidate_EmptyTarget_ClampsCapacityWithoutMutation()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 5),
                _slots[1],
                out var candidate);

            Assert.IsTrue(accepted);
            Assert.AreSame(_slots[1], candidate.Slot);
            Assert.AreEqual(3, candidate.RemainingCapacity);
            Assert.AreEqual(2, _slots[0].Stack.Count);
            Assert.IsTrue(_slots[1].IsEmpty, "Candidate preview must not create a separate stack.");
        }

        [Test]
        public void TryGetCandidate_PartialTarget_ReportsRemainingCapacity()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(4, "gem"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 10),
                _slots[0],
                out var candidate);

            Assert.IsTrue(accepted);
            Assert.AreEqual(1, candidate.RemainingCapacity);
            Assert.AreEqual(4, _slots[0].Stack.Count);
        }

        [Test]
        public void GetCandidates_SourceIsReevaluatedOnEveryEnumeration()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem"));
            var geometry = new InventoryPlacementGeometry(_slots[0].Inventory);
            var source = _strategy.GetCandidates(geometry, MakeRequest("gem", 10));

            var first = source.ToList();
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var second = source.ToList();

            CollectionAssert.AreEqual(
                new[] { PlacementCandidateKind.Merge, PlacementCandidateKind.Create },
                first.Select(candidate => candidate.Kind));
            CollectionAssert.AreEqual(
                new[] { PlacementCandidateKind.Merge },
                second.Select(candidate => candidate.Kind));
        }

        [Test]
        public void CandidateOrderers_OnlyChangeAutomaticOrder()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem"));
            var request = MakeRequest("gem", 10);
            var source = _strategy.GetCandidates(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                request);

            var mergeFirst = MergeFirstPlacementCandidateOrderer.Instance
                .Order(source, request)
                .Select(candidate => candidate.Kind)
                .ToArray();
            var emptyFirst = EmptyFirstPlacementCandidateOrderer.Instance
                .Order(source, request)
                .Select(candidate => candidate.Kind)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { PlacementCandidateKind.Merge, PlacementCandidateKind.Create },
                mergeFirst);
            CollectionAssert.AreEqual(
                new[] { PlacementCandidateKind.Create, PlacementCandidateKind.Merge },
                emptyFirst);
        }

        // ---------- CanUseAlternativeSlot ----------

        [Test]
        public void CanUseAlternativeSlot_Empty_True()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            Assert.IsTrue(_strategy.CanUseAlternativeSlot(_slots[0], new FakeItemAdapter("gem")));
        }

        [Test]
        public void CanUseAlternativeSlot_PartialSameType_True()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));

            Assert.IsTrue(_strategy.CanUseAlternativeSlot(_slots[0], new FakeItemAdapter("gem")));
        }

        [Test]
        public void CanUseAlternativeSlot_FullSameType_False()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));

            Assert.IsFalse(_strategy.CanUseAlternativeSlot(_slots[0], new FakeItemAdapter("gem")));
        }

        [Test]
        public void CanUseAlternativeSlot_DifferentType_False()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));

            Assert.IsFalse(_strategy.CanUseAlternativeSlot(_slots[0], new FakeItemAdapter("gem")));
        }

        // ---------- GetSlotCandidates ----------

        [Test]
        public void CanAcceptItem_EmptySlot_ReturnsTrueAndSuggestsIt()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.AreSame(_slots[0], selection.Slot as BaseSlot);
        }

        [Test]
        public void CanAcceptItem_AllOccupiedDifferent_NoDynamic_False()
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
        public void CanAcceptItem_PartialSameTypeWithRoom_Suggested()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.AreSame(_slots[1], selection.Slot as BaseSlot);
        }

        [Test]
        public void CanAcceptItem_FullAndNoEmpty_DynamicPrefab_True()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock"));
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 1);

            var candidates = _strategy.GetSlotCandidates(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);
            var selection = _strategy.DefaultSlotSelectionPolicy.Select(candidates, request);

            Assert.IsTrue(selection.Accepted);
            Assert.IsTrue(selection.CreateNew, "Only dynamic capacity available — must be a forced-new selection");
        }

        // ---------- GetAcceptableCount ----------

        [Test]
        public void GetAcceptableCount_SumsEmptyAndPartialCapacity()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "gem"));  // room 2
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "rock")); // not counted
            // _slots[2] empty — room 5
            var request = MakeRequest("gem", 20);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(7, count);
        }

        [Test]
        public void GetAcceptableCount_ClampedToDesired()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            var request = MakeRequest("gem", 4);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: false, potentialNewSlots: 0, baseSlotPrefab: null);

            Assert.AreEqual(4, count);
        }

        [Test]
        public void GetAcceptableCount_IncludesPotentialNewSlots()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock")); // not matching, no capacity
            _prefab = TestSlotFactory.CreatePrefab();
            var request = MakeRequest("gem", 10);

            int count = _strategy.GetAcceptableCount(_slots, request, canCreateNewSlot: true, potentialNewSlots: 3, baseSlotPrefab: _prefab);

            // 3 new slots * 4 max = 12, clamped to desired 10
            Assert.AreEqual(10, count);
        }

        // ---------- helpers ----------

        private static InventoryAcceptanceRequest MakeRequest(string itemId, int desiredCount)
            => new InventoryAcceptanceRequest(
                targetInventory: null,
                itemAdapter: new FakeItemAdapter(itemId),
                desiredCount: desiredCount);
    }
}
