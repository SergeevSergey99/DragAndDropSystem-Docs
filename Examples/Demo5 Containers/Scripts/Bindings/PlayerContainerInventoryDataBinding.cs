using System.Collections.Generic;
using System.Linq;
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

            // An item already inside this container (dragged from its open view back onto its own
            // icon) is not a new insertion, so capacity does not apply — let the handler fire and
            // route a policy-driven drop into the container. Otherwise a full container would reject
            // here and the normal pipeline would leak the item out into the player inventory.
            bool alreadyInside = container.Items.Contains(sourceAdapter.Instance);
            if (!alreadyInside && container.Items.Count >= container.Item.Capacity)
                return false;

            if (sourceAdapter.Instance is ContainerItemInstance dragged && WouldCreateCycle(dragged, container))
                return false;

            return true;
        }

        public OccupiedSlotDropResult ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot)
        {
            if (occupiedBaseSlot?.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter { Instance: ContainerItemInstance container })
                return OccupiedSlotDropResult.Rejected;

            if (entry.Stack?.PrimaryAdapter is not ContainerItemAdapterAdapter)
                return OccupiedSlotDropResult.Rejected;

            return ContainerViewRegistry.AutoCreateInstance.InsertIntoContainer(entry, container, occupiedBaseSlot)
                ? OccupiedSlotDropResult.Handled
                : OccupiedSlotDropResult.Rejected;
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
