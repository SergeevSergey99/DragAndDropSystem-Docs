using System;
using UniversalDragAndDrop.Slots;
using UnityEngine;
using UnityEngine.EventSystems;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Interaction
{
    [Serializable]
    public abstract class SlotInteractionAction
    {
        public virtual string DisplayName => GetType().Name.Replace("Action", string.Empty);
        
        public virtual bool IsDragBinding() => false;

        public virtual bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => inventory != null;

        public virtual ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => ActionResult.Failed("Action is not implemented");
    }

    [Serializable]
    public abstract class AssetSafeSlotInteractionAction : SlotInteractionAction {}

    [Serializable]
    public sealed class DragSlotAction : AssetSafeSlotInteractionAction
    {
        [SerializeField, Tooltip("Temporary item amount override for the current StartDrag. Does not change the inventory default.")]
        private DragRequestPolicySettings _dragPolicyOverride = new DragRequestPolicySettings();

        public override bool IsDragBinding() => true;
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return true;

            var slot = adapter?.BaseSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
            {
                DragAndDropManager.AutoCreateInstance.CompleteDrag(null);
                return ActionResult.Succeeded();
            }

            var slot = adapter?.BaseSlot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            return DragAndDropManager.AutoCreateInstance.StartDrag(slot, _dragPolicyOverride.TryBuild())
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Start drag failed");
        }
    }

    [Serializable]
    public sealed class CompleteDragAction : AssetSafeSlotInteractionAction
    {
        [field: SerializeField] public bool CancelOnNoSlots { get; private set; } = true;
        [SerializeField] private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        public override bool IsDragBinding() => true;
        
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            if (CancelOnNoSlots && !DragAndDropManager.AutoCreateInstance.HasActiveDropTarget)
            {
                DragAndDropManager.AutoCreateInstance.CancelDrag();
                return ActionResult.Succeeded();
            }

            DragAndDropManager.AutoCreateInstance.CompleteDrag(_dropPolicyOverride.TryBuild());
            return ActionResult.Succeeded();
        }
    }

    [Serializable]
    public sealed class SplitDropAction : AssetSafeSlotInteractionAction
    {
        [SerializeField, Min(1), Tooltip("Number of items to split off and drop.")]
        private int _splitCount = 1;
        [SerializeField] private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        public override bool IsDragBinding() => true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            return DragAndDropManager.AutoCreateInstance.SplitDrop(_dropPolicyOverride.TryBuild(), _splitCount)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Split drop failed");
        }
    }

    [Serializable]
    public sealed class CancelDragAction : AssetSafeSlotInteractionAction
    {
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            DragAndDropManager.AutoCreateInstance.CancelDrag();
            return ActionResult.Succeeded();
        }
    }

    [Serializable]
    public sealed class InventorySlotAction : SlotInteractionAction
    {
        [SerializeField, Tooltip("Scene action component (MonoBehaviour).")]
        private InventoryActionBase _sceneAction;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_sceneAction == null || inventory == null)
                return false;

            var slot = adapter?.BaseSlot ?? inventory.ResolveAutoTransferSlot();
            return _sceneAction.CanExecute(inventory, slot);
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_sceneAction == null || inventory == null)
                return ActionResult.Failed("Inventory action is not configured");

            var slot = adapter?.BaseSlot ?? inventory.ResolveAutoTransferSlot();
            if (!_sceneAction.CanExecute(inventory, slot))
                return ActionResult.Failed("Inventory action cannot execute");

            return _sceneAction.Execute(inventory, slot);
        }
    }
}
