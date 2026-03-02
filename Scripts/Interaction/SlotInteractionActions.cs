using System;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public abstract class SlotInteractionAction
    {
        public virtual string DisplayName => GetType().Name.Replace("Action", string.Empty);

        public virtual bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => inventory != null;

        public virtual void Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData) {}

        public virtual bool CanExecute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
            => CanExecute(
                coordinator != null ? coordinator.Inventory : adapter?.Slot?.Inventory as UniversalInventory,
                adapter,
                eventData);
    }

    [Serializable]
    public sealed class DragSlotAction : SlotInteractionAction
    {
        [field: SerializeField] public bool CompleteOnPointerUp { get; private set; } = true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.Instance.IsDragging)
                return CompleteOnPointerUp;

            var slot = adapter?.Slot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override void Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.Instance.IsDragging)
            {
                if (!CompleteOnPointerUp)
                    return;

                DragAndDropManager.Instance.CompleteDrag();
                return;
            }

            var slot = adapter?.Slot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return;

            DragAndDropManager.Instance.StartDrag(slot);
        }
    }

    [Serializable]
    public sealed class CompleteDragAction : SlotInteractionAction
    {
        [field: SerializeField] public bool CancelOnNoSlots { get; private set; } = true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.Instance.IsDragging;

        public override void Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.Instance.IsDragging)
                return;

            if (CancelOnNoSlots && !DragAndDropManager.Instance.HasActiveDropTarget)
            {
                DragAndDropManager.Instance.CancelDrag();
                return;
            }

            DragAndDropManager.Instance.CompleteDrag();
        }
    }

    [Serializable]
    public sealed class CancelDragAction : SlotInteractionAction
    {
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.Instance.IsDragging;

        public override void Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.Instance.IsDragging)
                return;

            DragAndDropManager.Instance.CancelDrag();
        }
    }

    [Serializable]
    public sealed class SelectionSlotAction : SlotInteractionAction
    {
        [SerializeReference] private SelectionOperationBase _operation;

        public SelectionSlotAction()
        {
        }

        public SelectionSlotAction(SelectionOperationBase operation)
        {
            _operation = operation;
        }

        public override bool CanExecute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_operation == null)
                return false;

            if (!SelectionManager.IsInstanceExist)
                return false;

            return _operation.CanExecute(SelectionManager.Instance, adapter?.Slot);
        }

        public override void Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_operation == null || !SelectionManager.IsInstanceExist)
                return;

            var manager = SelectionManager.Instance;
            var slot = adapter?.Slot;
            if (_operation.CanExecute(manager, slot))
            {
                _operation.Execute(manager, slot);
            }
        }
    }

    [Serializable]
    public sealed class InventorySlotAction : SlotInteractionAction
    {
        [SerializeField] private InventoryActionBase _action;

        public InventorySlotAction()
        {
        }

        public InventorySlotAction(InventoryActionBase action)
        {
            _action = action;
        }

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_action == null || inventory == null)
                return false;

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            return _action.CanExecute(inventory, slot);
        }

        public override void Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_action == null || inventory == null)
                return;

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            if (!_action.CanExecute(inventory, slot))
                return;

            _action.Execute(inventory, slot);
        }
    }
}
