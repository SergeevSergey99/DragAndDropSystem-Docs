using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
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
        [SerializeField, Tooltip("Временный override количества предметов для текущего StartDrag. Не меняет inventory default.")]
        private DragRequestPolicySettings _dragPolicyOverride = new DragRequestPolicySettings();

        public override bool IsDragBinding() => true;
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return true;

            var slot = adapter?.Slot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
            {
                DragAndDropManager.AutoCreateInstance.CompleteDrag(null);
                return ActionResult.Succeeded();
            }

            var slot = adapter?.Slot;
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
        [SerializeField, Tooltip("Сценовый action-компонент (MonoBehaviour).")]
        private InventoryActionBase _sceneAction;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_sceneAction == null || inventory == null)
                return false;

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            return _sceneAction.CanExecute(inventory, slot);
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_sceneAction == null || inventory == null)
                return ActionResult.Failed("Inventory action is not configured");

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            if (!_sceneAction.CanExecute(inventory, slot))
                return ActionResult.Failed("Inventory action cannot execute");

            return _sceneAction.Execute(inventory, slot);
        }
    }
}
