using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Inventories;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Selection
{
    [Serializable]
    public sealed class SelectionSlotAction : AssetOnlySlotInteractionAction
    {
        [UnityEngine.SerializeReference] private SelectionOperationBase _operation;

        public SelectionSlotAction() {}

        public SelectionSlotAction(SelectionOperationBase operation)
        {
            _operation = operation;
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
}
