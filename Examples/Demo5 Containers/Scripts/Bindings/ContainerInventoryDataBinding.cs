using System.Collections.Generic;
using System.Linq;
using UDND.Core;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Examples.Containers
{
    public class ContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapterAdapter>, IPreRuleOccupiedSlotDropHandler
    {
        public ContainerItemInstance currentContainer { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            ContainerViewRegistry.AutoCreateInstance.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (ContainerViewRegistry.IsInstanceExist)
                ContainerViewRegistry.Instance.Unregister(this);
        }

        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => currentContainer.Items;
        protected override ContainerItemAdapterAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);
        protected override void AddToData(ContainerItemAdapterAdapter adapterAdapter) => currentContainer.AddItem(adapterAdapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapterAdapter adapterAdapter) => currentContainer.RemoveItem(adapterAdapter.Instance);
        
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

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is ContainerItemAdapterAdapter { Instance: ContainerItemInstance draggedContainer } && WouldCreateCycle(draggedContainer, currentContainer))
                    return RuleResult.Failure("Container cannot be placed into itself or its child container");

            return base.CanDrop(context, entry);
        }
        
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
