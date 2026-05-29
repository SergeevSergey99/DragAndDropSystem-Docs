using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.DataBinding;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Unity-serializable base type for inventory components.
    /// Runtime services should keep depending on <see cref="IInventory"/> unless they need a serialized MonoBehaviour reference.
    /// </summary>
    public abstract class BaseInventory : MonoBehaviour, IInventory
    {
        public abstract IReadOnlyList<BaseSlot> Slots { get; }
        public abstract int SlotCount { get; }
        public abstract InventoryDataBindingBase DataBinding { get; protected set; }
        public abstract Transform SlotContainer { get; }

        public abstract event Action<BaseSlot> OnSlotCreated;
        public abstract event Action<InventoryItemEventContext> OnItemAdded;
        public abstract event Action<InventoryItemEventContext> OnItemRemoved;
        public abstract event Action OnContentRefreshed;
        public abstract event Action<InventorySwapContext> OnSwapAttempting;
        public abstract event Action<InventorySwapContext> OnSwapCompleted;

        public abstract void Initialize(InventoryDataBindingBase inventoryDataBindingBase);
        public abstract void SetMaxStackSize(int maxStackSize, bool allowItemOverride = false);
        public abstract void SetDragAmountStep(int step, DragAmountStepRounding rounding = DragAmountStepRounding.Floor);
        public abstract void NotifyContentRefreshed();
        public abstract void ReInitSlots(int slotCount);
        public abstract void ClearAll();

        public abstract BaseSlot GetSlot(int index);
        public abstract bool TryAddStack(ItemStack stack, int targetSlotIndex = -1);
        public abstract bool TryAddStackQuiet(ItemStack stack, int targetSlotIndex = -1);
        public abstract bool Contains(IItemAdapter itemAdapter);
        public abstract void UpdateAllVisuals();
        public abstract int GetDragAmount(BaseSlot baseSlot, DragAmount? overrideAmount = null, int? overrideCustom = null);
        public abstract bool TryAddToSlot(
            ItemStack stack,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory = null,
            int sourceSlotIndex = -1,
            SlotOperationContext operationContext = null);
        public abstract int RemoveItemsFromSlot(
            BaseSlot sourceBaseSlot,
            ItemStack stackToRemove,
            IInventory targetInventory = null,
            BaseSlot targetBaseSlot = null);
        public abstract int GetAcceptableCount(InventoryAcceptanceRequest request);

        public abstract bool CanAcceptItem(
            InventoryAcceptanceRequest request,
            out BaseSlot suggestedBaseSlot);

        public abstract bool TryGetStackForSlot(BaseSlot baseSlot, out IReadOnlyItemStack stack);
        public abstract bool TrySetStackForSlot(BaseSlot baseSlot, ItemStack stack);
        public abstract bool TryClearSlot(BaseSlot baseSlot);
        public abstract bool TryGetPlacementAt(BaseSlot baseSlot, out Placement placement);
        public abstract Vector2Int GetGrabOffset(Placement placement, BaseSlot baseSlot);
        public abstract bool TrySplitFromSlot(BaseSlot baseSlot, int amount, out ItemStack splitStack);
        public abstract bool TryAddToSlotStack(BaseSlot baseSlot, ItemStack stack);
        public abstract bool TryRemoveFromSlot(BaseSlot baseSlot, IReadOnlyList<IItemAdapter> adapters, out int removed);
    }
}
