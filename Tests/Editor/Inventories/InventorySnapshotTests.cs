using NUnit.Framework;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Tests.Inventories
{
    [TestFixture]
    public class InventorySnapshotTests
    {
        [Test]
        public void SlotState_IsIndependent_OfSubsequentSplit()
        {
            // Regression: InventorySlotState previously stored a direct reference to
            // ItemStack's internal adapter list. Split/RemoveFromStack mutated the list
            // in place, so snapshots captured before the mutation would "empty out"
            // and rollback would Clear the slot instead of restoring it.
            var stack = ItemStackBuilder.Unique(5);
            var state = new InventorySlotState(stack.Adapters);

            stack.Split(4);

            Assert.AreEqual(5, state.Count, "Snapshot count must not follow live stack mutations");
            Assert.IsFalse(state.IsEmpty);
            Assert.IsNotNull(state.ItemAdapter);
        }

        [Test]
        public void SlotState_IsIndependent_OfFullRemoval()
        {
            var stack = ItemStackBuilder.Unique(3);
            var state = new InventorySlotState(stack.Adapters);

            stack.RemoveFromStack(3);

            Assert.AreEqual(3, state.Count);
            Assert.IsFalse(state.IsEmpty);
        }

        [Test]
        public void SlotState_FromNullAdapters_IsEmpty()
        {
            var state = new InventorySlotState(null);

            Assert.IsTrue(state.IsEmpty);
            Assert.AreEqual(0, state.Count);
            Assert.IsNull(state.ItemAdapter);
        }
    }
}
