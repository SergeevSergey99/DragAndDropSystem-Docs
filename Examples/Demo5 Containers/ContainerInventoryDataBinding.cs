using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Examples.Containers.UI;
using DragAndDropSystem.Rules;
using UnityEngine;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Examples.Containers
{
    public class ContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapter>
    {
        public ContainerItemInstance currentContainer { get; private set; }

        #region Overrides
        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => currentContainer.Items;
        protected override ContainerItemAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);
        protected override void AddToData(ContainerItemAdapter adapter) => currentContainer.AddItem(adapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapter adapter) => currentContainer.RemoveItem(adapter.Instance);
        
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.Item is ContainerItemAdapter { Instance: ContainerItemInstance draggedContainer } && WouldCreateCycle(draggedContainer, currentContainer))
                    return RuleResult.Failure("Container cannot be placed into itself or its child container");

            return base.CanDrop(context, entry);
        }
        #endregion
        
        public void SetContainer(ContainerItemInstance container, bool refreshUI = true)
        {
            currentContainer = container;
            ResizeInventory(container?.Item?.Capacity ?? 0);

            if (refreshUI)
                ReloadUI();
        }

        private void ResizeInventory(int desiredSlotCount)
        {
            if (Inventory == null)
                return;

            var slots = new List<InventorySlotState>(desiredSlotCount);
            for (int i = 0; i < desiredSlotCount; i++)
                slots.Add(new InventorySlotState(null, 0));

            Inventory.RestoreSnapshot(new InventorySnapshot(slots));
            Inventory.UpdateAllVisuals();
        }
        

        private static bool WouldCreateCycle(ContainerItemInstance draggedContainer, ContainerItemInstance owner)
        {
            if (draggedContainer == owner)
                return true;

            foreach (var item in owner.Items)
            {
                if (item is ContainerItemInstance childContainer && WouldCreateCycle(draggedContainer, childContainer))
                    return true;
            }
            return false;
        }
    }
}
