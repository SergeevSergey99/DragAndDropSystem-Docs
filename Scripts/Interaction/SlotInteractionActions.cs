using System;
using DragAndDropSystem.Core;
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

        public virtual ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => ActionResult.Failed("Action is not implemented");

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

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.Instance.IsDragging)
            {
                if (!CompleteOnPointerUp)
                    return ActionResult.Failed("Complete on pointer up is disabled");

                DragAndDropManager.Instance.CompleteDrag();
                return ActionResult.Succeeded();
            }

            var slot = adapter?.Slot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            return DragAndDropManager.Instance.StartDrag(slot)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Start drag failed");
        }
    }

    [Serializable]
    public sealed class CompleteDragAction : SlotInteractionAction
    {
        [field: SerializeField] public bool CancelOnNoSlots { get; private set; } = true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.Instance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.Instance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            if (CancelOnNoSlots && !DragAndDropManager.Instance.HasActiveDropTarget)
            {
                DragAndDropManager.Instance.CancelDrag();
                return ActionResult.Succeeded();
            }

            DragAndDropManager.Instance.CompleteDrag();
            return ActionResult.Succeeded();
        }
    }

    [Serializable]
    public sealed class CancelDragAction : SlotInteractionAction
    {
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.Instance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.Instance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            DragAndDropManager.Instance.CancelDrag();
            return ActionResult.Succeeded();
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

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_operation == null || !SelectionManager.IsInstanceExist)
                return ActionResult.Failed("Selection operation is not available");

            var manager = SelectionManager.Instance;
            var slot = adapter?.Slot;
            if (_operation.CanExecute(manager, slot))
            {
                _operation.Execute(manager, slot);
                return ActionResult.Succeeded();
            }

            return ActionResult.Failed("Selection operation cannot execute");
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

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_action == null || inventory == null)
                return ActionResult.Failed("Inventory action is not configured");

            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            if (!_action.CanExecute(inventory, slot))
                return ActionResult.Failed("Inventory action cannot execute");

            return _action.Execute(inventory, slot);
        }
    }
}
