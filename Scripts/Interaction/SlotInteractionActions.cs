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

        public virtual bool Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData, bool logWarnings)
            => false;

        public virtual bool CanExecute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
            => CanExecute(
                coordinator != null ? coordinator.Inventory : adapter?.Slot?.Inventory as UniversalInventory,
                adapter,
                eventData);

        public virtual void Execute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            Execute(
                coordinator != null ? coordinator.Inventory : adapter?.Slot?.Inventory as UniversalInventory,
                adapter,
                eventData,
                logWarnings: false);
        }
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

        public override bool Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData, bool logWarnings)
        {
            if (DragAndDropManager.Instance.IsDragging)
            {
                if (!CompleteOnPointerUp)
                    return false;

                DragAndDropManager.Instance.CompleteDrag();
                return true;
            }

            var slot = adapter?.Slot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return false;

            return DragAndDropManager.Instance.StartDrag(slot);
        }
    }

    [Serializable]
    public sealed class CompleteDragAction : SlotInteractionAction
    {
        [field: SerializeField] public bool CancelOnNoSlots { get; private set; } = true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.Instance.IsDragging;

        public override bool Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData, bool logWarnings)
        {
            if (!DragAndDropManager.Instance.IsDragging)
                return false;

            if (CancelOnNoSlots && !DragAndDropManager.Instance.HasActiveDropTarget)
            {
                DragAndDropManager.Instance.CancelDrag();
                return true;
            }

            DragAndDropManager.Instance.CompleteDrag();
            return true;
        }
    }

    [Serializable]
    public sealed class CancelDragAction : SlotInteractionAction
    {
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.Instance.IsDragging;

        public override bool Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData, bool logWarnings)
        {
            if (!DragAndDropManager.Instance.IsDragging)
                return false;

            DragAndDropManager.Instance.CancelDrag();
            return true;
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

        public override bool Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData, bool logWarnings)
        {
            if (_operation == null || !SelectionManager.IsInstanceExist)
                return false;

            var manager = SelectionManager.Instance;
            var slot = adapter?.Slot;
            if (_operation.CanExecute(manager, slot))
            {
                _operation.Execute(manager, slot);
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class InventorySlotAction : SlotInteractionAction
    {
        [SerializeField] private InventoryActionBase _action;
        [SerializeField] private bool _logWarnings;

        public InventorySlotAction()
        {
        }

        public InventorySlotAction(InventoryActionBase action, bool logWarnings = false)
        {
            _action = action;
            _logWarnings = logWarnings;
        }

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_action == null || inventory == null)
                return false;

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            return _action.CanExecute(inventory, slot);
        }

        public override bool Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData, bool logWarnings)
        {
            if (_action == null || inventory == null)
                return false;

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            if (!_action.CanExecute(inventory, slot))
                return false;

            return _action.Execute(inventory, slot, _logWarnings || logWarnings);
        }
    }
}
