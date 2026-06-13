using System.Collections.Generic;
using System.Linq;
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
        private UniversalInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _strategy = new UniqueItemStrategy();
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

        private UniversalInventory BuildInventory(int slotCount)
        {
            _inventory = new InventoryBuilder()
                .WithStrategy(_strategy)
                .WithFixedSlots(slotCount)
                .Build();
            _slots = new List<BaseSlot>(_inventory.Slots);
            return _inventory;
        }

        // ---------- TryAdd with explicit targetIndex ----------

        [Test]
        public void TryAdd_TargetEmpty_PlacesExactlyOneItem()
        {
            var inventory = BuildInventory(3);
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack, 1);

            Assert.IsFalse(fullyPlaced, "Unique places 1 per call when addressed by index");
            Assert.IsFalse(inventory.GetSlot(1).IsEmpty);
            Assert.AreEqual(1, inventory.GetSlot(1).Stack.Count);
            Assert.AreEqual(1, stack.Count, "One item must remain in the source stack");
        }

        [Test]
        public void TryAdd_TargetOccupied_DoesNothing()
        {
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "existing"));
            var incoming = ItemStackBuilder.Unique(1, "gem");

            bool fullyPlaced = inventory.TryAddStack(incoming, 0);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual("existing", inventory.GetSlot(0).Stack.ID);
            Assert.AreEqual(1, incoming.Count);
        }

        // ---------- TryAdd distribution across empty slots ----------

        [Test]
        public void TryAdd_NoTargetIndex_DistributesOnePerEmptySlot()
        {
            var inventory = BuildInventory(3);
            var stack = ItemStackBuilder.Unique(3, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsTrue(fullyPlaced);
            Assert.IsTrue(stack.IsEmpty);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(1, inventory.GetSlot(i).Stack.Count);
        }

        [Test]
        public void TryAdd_FiveItems_FourEmptySlots_LeavesOneInStack()
        {
            // Regression: Unique must place 4 items, leave 1 in the source stack, and not crash.
            var inventory = BuildInventory(4);
            var stack = ItemStackBuilder.Unique(5, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual(1, stack.Count, "One item must remain un-placed");
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(1, inventory.GetSlot(i).Stack.Count);
        }

        [Test]
        public void TryAdd_NoEmptySlots_ReturnsFalseAndLeavesStackIntact()
        {
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "a"));
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Unique(1, "b"));
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool fullyPlaced = inventory.TryAddStack(stack);

            Assert.IsFalse(fullyPlaced);
            Assert.AreEqual(2, stack.Count);
        }

        // ---------- TryRemove → TrySplitFromSlot ----------

        [Test]
        public void TryRemove_BySourceIndex_ClearsSlot()
        {
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Of(new FakeItemAdapter("gem")));

            bool removed = inventory.TrySplitFromSlot(inventory.GetSlot(1), 1, out _);

            Assert.IsTrue(removed);
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty);
        }

        [Test]
        public void TryRemove_WithoutIndex_ClearsFirstMatchingSlot()
        {
            var inventory = BuildInventory(3);
            inventory.TrySetStackForSlot(inventory.GetSlot(1), ItemStackBuilder.Unique(1, "gem"));
            inventory.TrySetStackForSlot(inventory.GetSlot(2), ItemStackBuilder.Unique(1, "gem"));

            var gemAdapter = new FakeItemAdapter("gem");
            bool removed = false;
            foreach (var slot in inventory.Slots)
            {
                if (slot.IsEmpty || !slot.Stack.CanStack(gemAdapter)) continue;
                removed = inventory.TrySplitFromSlot(slot, 1, out _);
                break;
            }

            Assert.IsTrue(removed);
            Assert.IsTrue(inventory.GetSlot(1).IsEmpty);
            Assert.IsFalse(inventory.GetSlot(2).IsEmpty, "Only the first match should be cleared");
        }

        [Test]
        public void TryRemove_NoMatch_ReturnsFalse()
        {
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "gem"));

            var gemSlot = inventory.GetSlot(0);
            var rockAdapter = new FakeItemAdapter("rock");
            bool hasRock = !gemSlot.IsEmpty && gemSlot.Stack.CanStack(rockAdapter);

            Assert.IsFalse(hasRock, "No rock found — nothing to remove");
            Assert.IsFalse(inventory.GetSlot(0).IsEmpty, "Gem slot is unchanged");
        }

        // ---------- TryAddToSlot ----------

        [Test]
        public void TryAddToSlot_EmptyTarget_PlacesOneItem()
        {
            var inventory = BuildInventory(2);
            var stack = ItemStackBuilder.Unique(2, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsTrue(placed);
            Assert.AreEqual(1, inventory.GetSlot(0).Stack.Count);
            Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void TryAddToSlot_OccupiedTarget_ReturnsFalse()
        {
            var inventory = BuildInventory(2);
            inventory.TrySetStackForSlot(inventory.GetSlot(0), ItemStackBuilder.Unique(1, "existing"));
            var stack = ItemStackBuilder.Unique(1, "gem");

            bool placed = inventory.TryAddToSlot(stack, inventory.GetSlot(0));

            Assert.IsFalse(placed);
            Assert.AreEqual("existing", inventory.GetSlot(0).Stack.ID);
            Assert.AreEqual(1, stack.Count);
        }

        // ---------- TryGetCandidate ----------

        [Test]
        public void TryGetCandidate_EmptyTarget_ReturnsSingleItemWithoutMutation()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            var request = MakeRequest("gem", 5);

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                request,
                _slots[1],
                out var candidate);

            Assert.IsTrue(accepted);
            Assert.AreSame(_slots[1], candidate.Anchor);
            Assert.AreEqual(1, candidate.Capacity);
            Assert.IsTrue(_slots[1].IsEmpty, "Candidate preview must not mutate the target.");
        }

        [Test]
        public void TryGetCandidate_OccupiedTarget_Rejects()
        {
            _slots = TestSlotFactory.CreateSlots(1);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "existing"));

            bool accepted = _strategy.TryGetCandidate(
                new InventoryPlacementGeometry(_slots[0].Inventory),
                MakeRequest("gem", 1),
                _slots[0],
                out _);

            Assert.IsFalse(accepted);
            Assert.AreEqual("existing", _slots[0].Stack.ID);
        }

        // ---------- GetCandidates ----------

        [Test]
        public void CanAcceptItem_HasEmptySlot_ReturnsTrueAndSuggestsIt()
        {
            _slots = TestSlotFactory.CreateSlots(3);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsNotEmpty(candidates);
            Assert.AreSame(_slots[1], candidates[0].Anchor);
        }

        [Test]
        public void CanAcceptItem_AllFullAndNoDynamic_ReturnsFalse()
        {
            _slots = TestSlotFactory.CreateSlots(2);
            _slots[0].SetStack(ItemStackBuilder.Unique(1, "a"));
            _slots[1].SetStack(ItemStackBuilder.Unique(1, "b"));
            var request = MakeRequest("gem", 1);

            var candidates = GetCandidates(request);

            Assert.IsEmpty(candidates);
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

        private List<PlacementCandidate> GetCandidates(InventoryAcceptanceRequest request)
            => _strategy
                .GetCandidates(new InventoryPlacementGeometry(_slots[0].Inventory), request)
                .ToList();
    }
}
