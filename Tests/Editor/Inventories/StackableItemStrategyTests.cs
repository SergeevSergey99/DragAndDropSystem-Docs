using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private UniversalInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _strategy = new StackableItemStrategy();
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

        // The auto-merge toggle is a private serialized field with no public accessor (config via inspector).
        // Tests force explicit-merge-only via reflection.
        private static void SetExplicitMergeOnly(StackableItemStrategy strategy)
        {
            typeof(StackableItemStrategy)
                .GetField("_explicitMergeOnly", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(strategy, true);
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

        // ---------- TryAdd with limit ----------

        [Test]
        public void TryAdd_EmptyTarget_ClampsToMaxStackSize()
        {
            _strategy.SetMaxStackSize(3, allowItemOverride: false);
            var inventory = BuildInventory(2);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool placed = inventory.TryAddStack(stack, 0);

            Assert.IsFalse(placed);
            Assert.AreEqual(3, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, stack.Count, "Overflow stays in source when targetIndex is explicit");
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty, "Explicit-target TryAdd must not spill into other slots");
        }

        [Test]
        public void TryAdd_OccupiedTargetSameType_MergesUpToLimit()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(2, "gem"));
            var stack = ItemStackBuilder.Unique(5, "gem");

            inventory.TryAddStack(stack, 0);

            Assert.AreEqual(5, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void TryAdd_OccupiedTargetDifferentType_NoOp()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = inventory.TryAddStack(stack, 0);

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", inventory.GetSlot(0).Stack.ID);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void TryAdd_NoTarget_ItemPresent_FillsOnlyThatSlot_NoSpill()
        {
            // one-per-ID: an existing stack is topped up, but the overflow must NOT open a second stack.
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "gem")); // room for 3 more
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Unique(3, "rock"));
            // slot[2] empty
            var stack = ItemStackBuilder.Unique(6, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsFalse(fullyPlaced, "Overflow cannot open a second gem stack under one-per-ID");
            Assert.AreEqual(4, inventory.GetSlot(0).Stack.Count, "Existing gem stack filled to max");
            Assert.AreEqual("rock", inventory.GetSlot(1).Stack.ID, "Non-matching stack untouched");
            Assert.IsTrue(inventory.GetSlot(2).IsEmpty, "No spill into a second slot for the same item");
            Assert.AreEqual(3, stack.Count, "Overflow stays in the source stack");
        }

        [Test]
        public void TryAdd_NoTarget_ItemAbsent_PlacesIntoSingleEmptySlot()
        {
            _strategy.SetMaxStackSize(4, allowItemOverride: false);
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(3, "rock"));
            // slot[1], slot[2] empty
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(3, inventory.GetSlot(1).Stack.Count, "Absent item placed into first empty slot");
            Assert.IsTrue(inventory.GetSlot(2).IsEmpty, "Only one empty slot used");
        }

        [Test]
        public void TryAdd_NoTarget_Unlimited_PutsEverythingIntoOneSlot()
        {
            // Default _maxStackSize = 0 → unlimited
            var inventory = BuildInventory(3);
            var stack = ItemStackBuilder.Unique(50, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsTrue(fullyPlaced);
            Assert.AreEqual(50, inventory.GetSlot(0).Stack.Count);
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty);
            Assert.IsTrue(inventory.GetSlot(2).IsEmpty);
        }

        [Test]
        public void TryAdd_NoRoomAnywhere_LeavesStackIntact()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(2, "rock"));
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Unique(2, "rock"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual(3, stack.Count);
        }

        // ---------- TryRemove → TrySplitFromSlot ----------

        [Test]
        public void TryRemove_BySourceIndex_RemovesCountFromThatSlot()
        {
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(5, "gem"));

            bool removed = inventory.TrySplitFromSlot(inventory.GetSlot(0), 3, out var split);

            Assert.IsTrue(removed);
            Assert.AreEqual(3, split.Count);
            Assert.AreEqual(2, inventory.GetSlot(0).Stack.Count);
        }

        [Test]
        public void TryRemove_BySourceIndex_WrongType_StrategyRejects()
        {
            // Type-mismatch check is now at the acceptance (TryGetCandidate) level.
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(5, "gem"));

            var geometry = new InventoryPlacementGeometry(inventory);
            var rockRequest = new InventoryAcceptanceRequest(inventory, new FakeItemAdapter("rock"), 1);
            bool accepted = _strategy.TryGetCandidate(geometry, rockRequest, inventory.GetSlot(0), out _);

            Assert.IsFalse(accepted, "Strategy rejects merging 'rock' into slot holding 'gem'");
            Assert.AreEqual(5, inventory.GetSlot(0).Stack.Count, "Slot unchanged");
        }

        [Test]
        public void TryRemove_WithoutIndex_DistributesAcrossMatchingSlots()
        {
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(3, "gem"));
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Unique(1, "rock"));
            inventory.TrySetStackForSlot(inventory.GetSlot(2), ItemStackBuilder.Unique(4, "gem"));

            // Drain 5 gems across matching slots
            var gemAdapter = new FakeItemAdapter("gem");
            int remaining = 5;
            foreach (var slot in inventory.Slots)
            {
                if (slot.IsEmpty || !slot.Stack.CanStack(gemAdapter)) continue;
                int take = Math.Min(remaining, slot.Stack.Count);
                inventory.TrySplitFromSlot(slot, take, out _);
                remaining -= take;
                if (remaining == 0) break;
            }

            Assert.AreEqual(0, remaining, "All 5 gems removed");
            Assert.IsTrue(inventory.GetSlot(0).IsEmpty, "First gem slot fully drained");
            Assert.AreEqual("rock", inventory.GetSlot(1).Stack.ID, "Non-matching slot untouched");
            Assert.AreEqual(2, inventory.GetSlot(2).Stack.Count, "Remaining 2 in second gem slot");
        }

        [Test]
        public void TryRemove_NoMatch_ReturnsFalse()
        {
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(2, "gem"));

            var geometry = new InventoryPlacementGeometry(inventory);
            var rockRequest = new InventoryAcceptanceRequest(inventory, new FakeItemAdapter("rock"), 1);
            var candidates = _strategy.GetCandidates(geometry, rockRequest).ToList();

            Assert.IsEmpty(candidates, "No slot can accept 'rock' when slots contain only 'gem'");
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

                Assert.IsTrue(inventory.Contains(new ShapeAdapter("bag", 2, 2)));
                int count = inventory.Placements.Count(p => p?.Stack != null && p.Stack.CanStack(new ShapeAdapter("bag", 2, 2)));
                Assert.AreEqual(1, count);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void GetCandidates_ShapedPlacementWithRoom_ReportsRemainingCapacity()
        {
            // C2 (ShapedStacking-Plan.md): a shaped item is no longer capped at 1. An existing shaped
            // placement is a stackable one-per-ID location with capacity = maxStack - count; no second
            // placement (and no new slot) is offered for an item that already exists.
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            var inventory = new InventoryBuilder()
                .WithStrategy(_strategy)
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();
            _inventory = inventory;
            _slots = new List<BaseSlot>(inventory.Slots);

            var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));
            Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));
            CollectionAssert.AreEqual(new[] { 0, 1 }, placement.CoveredIndices);

            // desiredCount=10 (> remaining capacity of 4) ensures candidate.Capacity reflects the true remainder.
            var request = new InventoryAcceptanceRequest(inventory, new ShapeAdapter("bag", 2, 1), 10);
            var candidates = _strategy
                .GetCandidates(new InventoryPlacementGeometry(inventory), request)
                .ToList();

            Assert.AreEqual(1, candidates.Count, "Only the existing placement's anchor is eligible");
            Assert.AreEqual(PlacementCandidateKind.Merge, candidates[0].Kind);
            Assert.AreEqual(0, candidates[0].Anchor.Index, "Candidate is the placement anchor");
            Assert.AreEqual(4, candidates[0].Capacity, "maxStack(5) - count(1)");
        }

        // ---------- TryAddToSlot ----------

        [Test]
        public void TryAddToSlot_EmptyTarget_PlacesUpToMaxStackSize()
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
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void TryAddToSlot_OccupiedDifferentType_ReturnsFalse()
        {
            var inventory = BuildInventory(1);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "rock"));
            var stack = ItemStackBuilder.Unique(1, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsFalse(placed);
            Assert.AreEqual("rock", inventory.GetSlot(0).Stack.ID);
        }

        [Test]
        public void TryAddToSlot_EmptyTarget_ItemExistsElsewhere_AutoMergesIntoExistingStack()
        {
            // Auto-merge ON (default): a duplicate dropped onto an empty slot consolidates into the existing
            // stack instead of opening a second stack or bouncing back to the source.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(1));

            Assert.IsTrue(placed);
            Assert.AreEqual(7, inventory.GetSlot(0).Stack.Count, "Merged into the existing stack");
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty, "Empty target stays empty (no second stack)");
            Assert.IsTrue(stack.IsEmpty, "Source consumed");
        }

        [Test]
        public void TryAddToSlot_EmptyTarget_ItemExistsElsewhere_ExplicitMergeOnly_Rejects()
        {
            // Explicit-merge-only: consolidation happens only on an explicit drop onto the stack, so a
            // duplicate dropped onto an empty slot is rejected (strict one-per-ID).
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            SetExplicitMergeOnly(_strategy);
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(4, "gem"));
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(1));

            Assert.IsFalse(placed, "Explicit-merge-only forbids consolidating via an empty slot");
            Assert.AreEqual(4, inventory.GetSlot(0).Stack.Count, "Existing stack untouched");
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty, "Empty target left empty");
            Assert.AreEqual(3, stack.Count, "Stack stays in source");
        }

        // ---------- TryGetCandidate ----------

        [Test]
        public void TryGetCandidate_ExplicitTarget_ReportsExactCapacityWithoutMutation()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(6, "gem"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 8),
                _slots[0],
                out var candidate);

            Assert.IsTrue(accepted);
            Assert.AreSame(_slots[0], candidate.Anchor);
            Assert.AreEqual(4, candidate.Capacity);
            Assert.AreEqual(6, _slots[0].Stack.Count, "Candidate preview must not mutate the target.");
        }

        [Test]
        public void TryGetCandidate_ExplicitMergeOnly_AllowsDirectExistingStack()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            SetExplicitMergeOnly(_strategy);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 3);

            var automaticCandidates = GetCandidates(request);
            bool explicitAccepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                request,
                _slots[0],
                out var candidate);

            Assert.IsEmpty(automaticCandidates);
            Assert.IsTrue(explicitAccepted);
            Assert.AreSame(_slots[0], candidate.Anchor);
            Assert.AreEqual(3, candidate.Capacity);
        }

        [Test]
        public void TryGetCandidate_EmptyTarget_AutoMergeRedirectsToExistingStack()
        {
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(7, "gem"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 5),
                _slots[1],
                out var candidate);

            Assert.IsTrue(accepted);
            Assert.AreSame(_slots[0], candidate.Anchor);
            Assert.AreEqual(3, candidate.Capacity);
            Assert.IsTrue(_slots[1].IsEmpty);
            Assert.AreEqual(7, _slots[0].Stack.Count);
        }

        // ---------- GetCandidates ----------

        [Test]
        public void CanAcceptItem_PartialSlotWithRoom_SuggestsIt()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 2);

            var candidates = GetCandidates(request);

            Assert.IsNotEmpty(candidates);
            Assert.AreSame(_slots[0], candidates[0].Anchor);
        }

        [Test]
        public void CanAcceptItem_AllFullAtLimit_ReturnsFalse()
        {
            _strategy.SetMaxStackSize(2, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            _slots[1].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsEmpty(candidates);
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

            var candidates = GetCandidates(request);

            Assert.IsEmpty(candidates, "Full existing stack + one-per-ID => no candidates, no new slot");
        }

        [Test]
        public void CanAcceptItem_OnlyDifferentItems_SuggestsEmptySlot()
        {
            _strategy.SetMaxStackSize(5, allowItemOverride: false);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(3, "rock"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsNotEmpty(candidates);
            Assert.AreSame(_slots[1], candidates[0].Anchor);
        }

        [Test]
        public void GetCandidates_SameInventorySourcePlacement_ExcludesAllCoveredCells()
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

                var candidates = _strategy
                    .GetCandidates(new InventoryPlacementGeometry(inventory), request)
                    .ToList();

                Assert.IsNotEmpty(candidates, "The source placement must not make the item look already present.");
                foreach (var candidate in candidates)
                {
                    Assert.AreNotEqual(0, candidate.Anchor.Index);
                    Assert.AreNotEqual(1, candidate.Anchor.Index);
                }
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void GetCandidates_AutoMergeOff_ItemPresent_ReturnsNone()
        {
            // C8: with auto-merge OFF, duplicates are not auto-consolidated via system distribution
            // (area-drop / auto-transfer / overflow). The existing stack accepts more only via an explicit
            // drop onto it, so automatic candidate enumeration offers nothing here.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            SetExplicitMergeOnly(_strategy);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "gem"));
            var request = MakeRequest("gem", 3);

            var candidates = GetCandidates(request);

            Assert.IsEmpty(candidates, "Auto-merge OFF must not auto-consolidate an existing stack");
        }

        [Test]
        public void GetCandidates_AutoMergeOff_ItemAbsent_StillOffersEmpty()
        {
            // Auto-merge OFF only changes duplicate handling; a first stack is still placed normally.
            _strategy.SetMaxStackSize(10, allowItemOverride: false);
            SetExplicitMergeOnly(_strategy);
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(2, "rock"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsNotEmpty(candidates);
            Assert.AreSame(_slots[1], candidates[0].Anchor);
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

        private List<PlacementCandidate> GetCandidates(InventoryAcceptanceRequest request)
            => _strategy
                .GetCandidates(new InventoryPlacementGeometry(_slots[0].Inventory), request)
                .ToList();

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
