using System.Collections.Generic;
using UDND.Core;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Examples.Containers
{
    public class PlayerContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapterAdapter>, IPreRuleOccupiedSlotDropHandler
    {
        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => ContainerDemoManager.AutoCreateInstance.Items;
        protected override ContainerItemAdapterAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);

        protected override void AddToData(ContainerItemAdapterAdapter adapterAdapter) => ContainerDemoManager.AutoCreateInstance.AddPlayerItem(adapterAdapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapterAdapter adapterAdapter) => ContainerDemoManager.AutoCreateInstance.RemovePlayerItem(adapterAdapter.Instance);

        public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
        {
            if (occupiedBaseSlot?.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter { Instance: ContainerItemInstance container })
                return false;

            if (entry.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter sourceAdapter)
                return false;

            if (container.Items.Count >= container.Item.Capacity)
                return false;

            if (sourceAdapter.Instance is ContainerItemInstance dragged && WouldCreateCycle(dragged, container))
                return false;

            return true;
        }

        public bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
        {
            if (occupiedBaseSlot?.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter { Instance: ContainerItemInstance container })
                return false;

            if (entry.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter sourceAdapter)
                return false;
            

            // Remove the dragged item from the inventory it actually came from, emitting that
            // inventory's removal events so its own DataBinding updates its backing data. Removing
            // it from THIS binding's player store instead leaves the item in its real source store,
            // so it reappears there on reload while also living in the container (duplication).
            var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
            sourceInventory?.RemoveItemsFromSlot(entry.SourceBaseSlot, entry.Stack);

            container.AddItem(sourceAdapter.Instance);
            
            Events.InvokeContainerContentChanged(container);
            return true;
        }

        private static bool WouldCreateCycle(ContainerItemInstance draggedContainer, ContainerItemInstance target)
        {
            if (draggedContainer == target)
                return true;

            foreach (var item in draggedContainer.Items)
            {
                if (item is ContainerItemInstance child && WouldCreateCycle(child, target))
                    return true;
            }
            return false;
        }
    }
}
