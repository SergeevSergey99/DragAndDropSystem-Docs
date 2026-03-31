using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Examples.Containers
{
    public class PlayerContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapterAdapter>
    {
        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => ContainerDemoManager.AutoCreateInstance.Items;
        protected override ContainerItemAdapterAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);

        protected override void AddToData(ContainerItemAdapterAdapter adapterAdapter) => ContainerDemoManager.AutoCreateInstance.AddPlayerItem(adapterAdapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapterAdapter adapterAdapter) => ContainerDemoManager.AutoCreateInstance.RemovePlayerItem(adapterAdapter.Instance);

        protected override bool CanHandleOccupiedSlotDrop(DragEntry entry, ISlot occupiedSlot)
        {
            if (occupiedSlot?.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter { Instance: ContainerItemInstance container })
                return false;

            if (entry.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter sourceAdapter)
                return false;

            if (container.Items.Count >= container.Item.Capacity)
                return false;

            if (sourceAdapter.Instance is ContainerItemInstance dragged && WouldCreateCycle(dragged, container))
                return false;

            return true;
        }

        protected override bool ExecuteOccupiedSlotDrop(DragEntry entry, ISlot occupiedSlot)
        {
            if (occupiedSlot?.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter { Instance: ContainerItemInstance container })
                return false;

            if (entry.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter sourceAdapter)
                return false;

            container.AddItem(sourceAdapter.Instance);
            ContainerDemoManager.AutoCreateInstance.RemovePlayerItem(sourceAdapter.Instance);

            entry.SourceSlot.Clear();
            entry.SourceSlot.UpdateVisuals();

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
