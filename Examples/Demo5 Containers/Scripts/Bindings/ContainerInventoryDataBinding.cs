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
    public class ContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapterAdapter>
    {
        public ContainerItemInstance currentContainer { get; private set; }

        #region Overrides
        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => currentContainer.Items;
        protected override ContainerItemAdapterAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);
        protected override void AddToData(ContainerItemAdapterAdapter adapterAdapter) => currentContainer.AddItem(adapterAdapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapterAdapter adapterAdapter) => currentContainer.RemoveItem(adapterAdapter.Instance);
        
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is ContainerItemAdapterAdapter { Instance: ContainerItemInstance draggedContainer } && WouldCreateCycle(draggedContainer, currentContainer))
                    return RuleResult.Failure("Container cannot be placed into itself or its child container");

            return base.CanDrop(context, entry);
        }
        #endregion
        
        public void SetContainer(ContainerItemInstance container)
        {
            currentContainer = container;
            ResizeInventory(container?.Item?.Capacity ?? 0);
            ReloadUI();
        }

        private void ResizeInventory(int desiredSlotCount)
        {
            if (Inventory == null)
                return;

            Inventory.ReInitSlots(desiredSlotCount);
        }
        

        private static bool WouldCreateCycle(ContainerItemInstance draggedContainer, ContainerItemInstance owner)
        {
            if (draggedContainer == owner)
                return true;

            foreach (var item in draggedContainer.Items)
            {
                if (item is ContainerItemInstance childContainer && WouldCreateCycle(childContainer, owner))
                    return true;
            }
            return false;
        }
    }
}
