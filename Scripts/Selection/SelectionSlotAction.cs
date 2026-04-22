using System;
using UnityEngine.EventSystems;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Interaction;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Selection
{
    [Serializable]
    public sealed class SelectionSlotAction : AssetSafeSlotInteractionAction
    {
        [UnityEngine.SerializeReference, ManagedReferencePicker] private SelectionOperationBase _operation;
        
        public override bool AllowOutOfSlot() => _operation.AllowOutOfSlot();

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_operation == null || !SelectionManager.IsInstanceExist)
                return ActionResult.Failed("Selection operation is not available");

            var manager = SelectionManager.AutoCreateInstance;
            var slot = adapter?.BaseSlot;
            if (_operation.CanExecute(manager, slot))
            {
                _operation.Execute(manager, slot);
                return ActionResult.Succeeded();
            }

            return ActionResult.Failed("Selection operation cannot execute");
        }
    }
}
