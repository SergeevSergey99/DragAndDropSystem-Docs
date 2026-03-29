using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Examples.Containers
{
    public class PlayerContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapter>
    {
        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => ContainerDemoManager.AutoCreateInstance.Items;
        protected override ContainerItemAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);

        protected override void AddToData(ContainerItemAdapter adapter) => ContainerDemoManager.AutoCreateInstance.AddPlayerItem(adapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapter adapter) => ContainerDemoManager.AutoCreateInstance.RemovePlayerItem(adapter.Instance);

        protected override bool CanHandleOccupiedSlotDrop(DragEntry entry, ISlot occupiedSlot)
        {
            if (occupiedSlot?.Stack?.Item is not ContainerItemAdapter { Instance: ContainerItemInstance container })
                return false;

            if (entry.Stack?.Item is not ContainerItemAdapter sourceAdapter)
                return false;

            if (container.Items.Count >= container.Item.Capacity)
                return false;

            if (sourceAdapter.Instance is ContainerItemInstance dragged && WouldCreateCycle(dragged, container))
                return false;

            return true;
        }

        protected override bool ExecuteOccupiedSlotDrop(DragEntry entry, ISlot occupiedSlot)
        {
            if (occupiedSlot?.Stack?.Item is not ContainerItemAdapter { Instance: ContainerItemInstance container })
                return false;

            if (entry.Stack?.Item is not ContainerItemAdapter sourceAdapter)
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
