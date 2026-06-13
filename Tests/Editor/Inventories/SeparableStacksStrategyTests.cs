using System;
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
        private UniversalInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _strategy = new SeparableStacksStrategy();
        }

        [TearDown]
        public void TearDown()
        {
            if (_inventory != null)
            {
                InventoryBuilder.Destroy(_inventory);
                _inventory = null;
            }
            else
            {
                TestSlotFactory.Dispose(_slots);
            }
            TestSlotFactory.Dispose(_prefab);
            _slots = null;
            _prefab = null;
        }

        private UniversalInventory BuildInventory(int slotCount, int maxStackSize = 0)
        {
            var builder = new InventoryBuilder()
                .WithStrategy(_strategy)
                .WithFixedSlots(slotCount);
            if (maxStackSize > 0)
                builder = builder.WithMaxStackSize(maxStackSize);
            _inventory = builder.Build();
            _slots = new List<BaseSlot>(_inventory.Slots);
            return _inventory;
        }

        // ---------- TryAdd with explicit target ----------

        [Test]
        public void TryAdd_EmptyTarget_ClampsToMaxStackSize()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            var inventory = BuildInventory(2);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool placed = inventory.TryAddStack(stack, 0);

            Assert.IsFalse(placed);
            Assert.AreEqual(3, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, stack.Count);
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty, "Explicit target must not spill into other slots");
        }

        [Test]
        public void TryAdd_OccupiedTargetSameType_MergesWhenAllowed()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(3, "gem"));
            var stack = ItemStackBuilder.Unique(4, "gem");

            inventory.TryAddStack(stack, 0);

            Assert.AreEqual(7, inventory.GetSlot(0).Stack.Count);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void TryAdd_OccupiedTargetDifferentType_Rejects()
        {
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = inventory.TryAddStack(stack, 0);

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", inventory.GetSlot(0).Stack.ID);
            Assert.AreEqual(2, stack.Count);
        }

        // ---------- TryAdd programmatic (no explicit target) ----------

        [Test]
        public void TryAdd_NoTarget_FillsExistingStackFirst_ThenEmptySlot()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(3, "gem")); // room 2
            // slot[1], slot[2] empty
            var stack = ItemStackBuilder.Unique(8, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(5, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(5, inventory.GetSlot(1).Stack.Count);
            Assert.AreEqual(1, inventory.GetSlot(2).Stack.Count);
        }

        [Test]
        public void TryAdd_NoTarget_SpillsAcrossEmptySlots_UntilStackSatisfied()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            var inventory = BuildInventory(3);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsTrue(fullyPlaced);
            Assert.AreEqual(2, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, inventory.GetSlot(1).Stack.Count);
            Assert.AreEqual(1, inventory.GetSlot(2).Stack.Count);
        }

        // ---------- TryRemove → TrySplitFromSlot ----------

        [Test]
        public void TryRemove_BySourceIndex_Removes()
        {
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(4, "gem"));

            bool removed = inventory.TrySplitFromSlot(inventory.GetSlot(0), 2, out _);

            Assert.IsTrue(removed);
            Assert.AreEqual(2, inventory.GetSlot(0).Stack.Count);
        }

        [Test]
        public void TryRemove_WithoutIndex_DrainsAcrossSeparateStacks()
        {
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(2, "gem"));
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Unique(3, "gem"));

            var gemAdapter = new FakeItemAdapter("gem");
            int remaining = 4;
            foreach (var slot in inventory.Slots)
            {
                if (slot.IsEmpty || !slot.Stack.CanStack(gemAdapter)) continue;
                int take = Math.Min(remaining, slot.Stack.Count);
                inventory.TrySplitFromSlot(slot, take, out _);
                remaining -= take;
                if (remaining == 0) break;
            }

            Assert.AreEqual(0, remaining);
            Assert.IsTrue(inventory.GetSlot(0).IsEmpty);
            Assert.AreEqual(1, inventory.GetSlot(1).Stack.Count);
        }

        // ---------- TryAddToSlot — KEY DIFFERENCE from Stackable ----------

        [Test]
        public void TryAddToSlot_EmptyTarget_DoesNotRedirectToExistingPartialStack()
        {
            // Core "separable" guarantee: drop on empty slot creates a NEW pile,
            // even if another slot has room for the same item. Two stacks coexist.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(1));

            Assert.IsTrue(placed);
            Assert.AreEqual(4, inventory.GetSlot(0).Stack.Count, "Existing stack must remain untouched");
            Assert.AreEqual(3, inventory.GetSlot(1).Stack.Count, "New separate pile created");
        }

        [Test]
        public void TryAddToSlot_EmptyTarget_ClampsToMaxStackSize()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            var inventory = BuildInventory(2);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsTrue(placed);
            Assert.AreEqual(3, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void TryAddToSlot_OccupiedSameType_Merges()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsTrue(placed);
            Assert.AreEqual(7, inventory.GetSlot(0).Stack.Count);
        }

        [Test]
        public void TryAddToSlot_OccupiedDifferentType_Rejects()
        {
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(1, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", inventory.GetSlot(0).Stack.ID);
        }

        [Test]
        public void TryAddToSlot_OccupiedAtCapacity_Rejects()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(3, "gem"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsFalse(placed);
            Assert.AreEqual(3, inventory.GetSlot(0).Stack.Count);
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
            Assert.AreSame(_slots[1], candidate.Anchor);
            Assert.AreEqual(3, candidate.Capacity);
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
            Assert.AreEqual(1, candidate.Capacity);
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

            var mergeFirst = new MergeFirstPlacementCandidateOrderer()
                .Order(source, request)
                .Select(candidate => candidate.Kind)
                .ToArray();
            var emptyFirst = new EmptyFirstPlacementCandidateOrderer()
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

        // ---------- CanUseAlternativeSlot → TryGetCandidate ----------

        [Test]
        public void CanUseAlternativeSlot_Empty_True()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 1),
                _slots[0],
                out _);
            Assert.IsTrue(accepted);
        }

        [Test]
        public void CanUseAlternativeSlot_PartialSameType_True()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 1),
                _slots[0],
                out _);
            Assert.IsTrue(accepted);
        }

        [Test]
        public void CanUseAlternativeSlot_FullSameType_False()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 1),
                _slots[0],
                out _);
            Assert.IsFalse(accepted);
        }

        [Test]
        public void CanUseAlternativeSlot_DifferentType_False()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 1),
                _slots[0],
                out _);
            Assert.IsFalse(accepted);
        }

        // ---------- GetCandidates ----------

        [Test]
        public void CanAcceptItem_EmptySlot_ReturnsTrueAndSuggestsIt()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "rock"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsNotEmpty(candidates);
            Assert.AreSame(_slots[0], candidates[0].Anchor);
        }

        [Test]
        public void CanAcceptItem_AllOccupiedDifferent_NoDynamic_False()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "b"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsEmpty(candidates);
        }

        [Test]
        public void CanAcceptItem_PartialSameTypeWithRoom_Suggested()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "rock"));
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsNotEmpty(candidates);
            Assert.AreSame(_slots[1], candidates[0].Anchor);
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

            int count = _strategy.GetAcceptableCount(new InventoryPlacementGeometry(_slots[0].Inventory), request);

            Assert.AreEqual(7, count);
        }

        [Test]
        public void GetAcceptableCount_ClampedToDesired()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(3);
            var request = MakeRequest("gem", 4);

            int count = _strategy.GetAcceptableCount(new InventoryPlacementGeometry(_slots[0].Inventory), request);

            Assert.AreEqual(4, count);
        }

        [Test]
        public void GetAcceptableCount_IncludesPotentialNewSlots()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            var inventory = new InventoryBuilder()
                .WithStrategy(_strategy)
                .WithFixedSlots(1)
                .WithSlotManagementSettings(new DynamicSlotManagementSettings())
                .Build();
            _inventory = inventory;
            _slots = new List<BaseSlot>(inventory.Slots);
            inventory.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "rock"));
            var request = new InventoryAcceptanceRequest(
                inventory,
                new FakeItemAdapter("gem"),
                10);

            int count = _strategy.GetAcceptableCount(new InventoryPlacementGeometry(inventory), request);

            // 3 new slots * 4 max = 12, clamped to desired 10
            Assert.AreEqual(10, count);
        }

        // ---------- helpers ----------

        private static InventoryAcceptanceRequest MakeRequest(string itemId, int desiredCount)
            => new InventoryAcceptanceRequest(
                targetInventory: null,
                itemAdapter: new FakeItemAdapter(itemId),
                desiredCount: desiredCount);

        private List<PlacementCandidate> GetCandidates(InventoryAcceptanceRequest request)
            => _strategy
                .GetCandidates(new InventoryPlacementGeometry(_slots[0].Inventory), request)
                .ToList();
    }
}
