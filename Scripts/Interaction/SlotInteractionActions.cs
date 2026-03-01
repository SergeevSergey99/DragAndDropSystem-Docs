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

        public virtual bool CanExecute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
            => coordinator != null;

        public abstract void Execute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData);
    }

    [Serializable]
    public sealed class DragSlotAction : SlotInteractionAction
    {
        public override void Execute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (coordinator == null)
                return;

            // Для pointer click-up не стартуем новый drag.
            // Старт drag на мыши происходит через BeginDrag.
            bool canStartFromThisEvent = eventData == null || eventData.dragging;

            if (coordinator.IsDragInProgress || canStartFromThisEvent)
            {
                coordinator.TryExecuteDrag(adapter?.Slot);
            }
        }
    }

    [Serializable]
    public sealed class CancelDragAction : SlotInteractionAction
    {
        public override void Execute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            coordinator.TryCancelDrag();
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
            if (!base.CanExecute(coordinator, adapter, eventData) || _operation == null)
                return false;

            if (!SelectionManager.IsInstanceExist)
                return false;

            return _operation.CanExecute(SelectionManager.Instance, adapter.Slot);
        }

        public override void Execute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_operation == null || !SelectionManager.IsInstanceExist)
                return;

            var manager = SelectionManager.Instance;
            if (_operation.CanExecute(manager, adapter.Slot))
            {
                _operation.Execute(manager, adapter.Slot);
            }
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

        public override bool CanExecute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!base.CanExecute(coordinator, adapter, eventData) || _action == null)
                return false;

            var inventory = coordinator.Inventory;
            if (inventory == null)
                return false;

            var slot = adapter?.Slot;
            return _action.CanExecute(inventory, slot);
        }

        public override void Execute(InventoryInteractionCoordinator coordinator, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_action == null || coordinator.Inventory == null)
                return;

            _action.Execute(coordinator.Inventory, adapter?.Slot, _logWarnings);
        }
    }
}
