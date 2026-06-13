using NUnit.Framework;
using UnityEngine;
using UDND.Inventories;

namespace UDND.Tests.Inventories
{
    /// <summary>
    /// Smoke tests for InventoryBuilder. These lock down the foundation that every
    /// subsequent integration test relies on: slot count, strategy wiring, prefab
    /// isolation (prefab GO must not be counted as a slot), and post-Awake state.
    /// </summary>
    [TestFixture]
    public class InventoryBuilderTests
    {
        private UniversalInventory _inventory;

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_inventory);
            _inventory = null;
        }

        [Test]
        public void Build_Default_CreatesInventoryWithExpectedSlotCount()
        {
            _inventory = new InventoryBuilder().WithFixedSlots(5).Build();

            Assert.IsNotNull(_inventory);
            Assert.AreEqual(5, _inventory.SlotCount);
            Assert.AreEqual(5, _inventory.Slots.Count);
        }

        [Test]
        public void Build_StrategyIsBoundAndInitialized()
        {
            var strategy = new UniqueItemStrategy();

            _inventory = new InventoryBuilder()
                .WithStrategy(strategy)
                .WithFixedSlots(2)
                .Build();

            Assert.IsNotNull(_inventory.Strategy, "Strategy must be initialized after Build");
            Assert.IsNotNull(_inventory.PlacementStrategy, "PlacementStrategy lazy-init must work");
            Assert.AreSame(strategy, _inventory.PlacementStrategy);
        }

        [Test]
        public void Build_AllSlotsAreInitializedAndEmpty()
        {
            _inventory = new InventoryBuilder().WithFixedSlots(3).Build();

            for (int i = 0; i < _inventory.SlotCount; i++)
            {
                var slot = _inventory.GetSlot(i);
                Assert.IsNotNull(slot, $"Slot {i} should not be null");
                Assert.AreEqual(i, slot.Index, $"Slot {i} must have the correct Index");
                Assert.IsTrue(slot.IsEmpty, $"Slot {i} must start empty");
                Assert.AreSame(_inventory, slot.Inventory, "Slot.Inventory must be the owner");
            }
        }

        [Test]
        public void Build_PrefabGameObject_IsNotCountedAsSlot()
        {
            // Regression guard: the prefab lives under the inventory root but outside
            // the slot container. CacheSlots must not pull it in.
            _inventory = new InventoryBuilder().WithFixedSlots(4).Build();

            Assert.AreEqual(4, _inventory.SlotCount,
                "SlotCount must equal requested count — prefab must not leak into Slots");
        }

        [Test]
        public void Build_ZeroSlots_CreatesInventoryWithEmptySlotList()
        {
            _inventory = new InventoryBuilder().WithFixedSlots(0).Build();

            Assert.AreEqual(0, _inventory.SlotCount);
            Assert.IsNotNull(_inventory.Slots);
        }

        [Test]
        public void Build_TryAddStack_PopulatesFirstSlot()
        {
            // End-to-end sanity: the assembled inventory actually works.
            _inventory = new InventoryBuilder()
                .WithStrategy(new StackableItemStrategy())
                .WithFixedSlots(3)
                .Build();

            var stack = ItemStackBuilder.Unique(2, "gem");

            bool added = _inventory.TryAddStack(stack);

            Assert.IsTrue(added);
            Assert.IsFalse(_inventory.GetSlot(0).IsEmpty);
            Assert.AreEqual(2, _inventory.GetSlot(0).Stack.Count);
        }

        [Test]
        public void Destroy_RemovesRootGameObject()
        {
            _inventory = new InventoryBuilder().WithFixedSlots(1).Build();
            var go = _inventory.gameObject;

            InventoryBuilder.Destroy(_inventory);
            _inventory = null;

            // DestroyImmediate flips the Unity-object equality to false
            Assert.IsTrue(go == null, "Root GameObject must be destroyed");
        }
    }
}
