using System;
using System.Collections.Generic;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Selection
{
    [Serializable]
    public sealed class StartMultiDragAction : AssetOnlySlotInteractionAction
    {
        [SerializeField] private bool _completeOnPointerUp = true;
        [SerializeField] private bool _fallbackToActiveSlotIfSelectionEmpty = true;
        [SerializeField] private bool _restrictToSameInventory = true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.Instance.IsDragging)
                return _completeOnPointerUp;

            var sourceSlots = BuildSourceSlots(inventory, adapter);
            return sourceSlots.Count > 0;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.Instance.IsDragging)
            {
                if (!_completeOnPointerUp)
                    return ActionResult.Failed("Complete on pointer up is disabled");

                DragAndDropManager.Instance.CompleteDrag();
                return ActionResult.Succeeded();
            }

            var sourceSlots = BuildSourceSlots(inventory, adapter);
            if (sourceSlots.Count == 0)
                return ActionResult.Failed("No valid slots for multi drag");

            return DragAndDropManager.Instance.StartDrag(sourceSlots)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Failed to start multi drag");
        }

        private List<ISlot> BuildSourceSlots(UniversalInventory inventory, SlotInputAdapter adapter)
        {
            var result = new List<ISlot>();
            var activeSlot = adapter?.Slot;

            if (SelectionManager.IsInstanceExist)
            {
                var context = SelectionManager.Instance.CurrentContext;
                if (context != null && context.HasSelection)
                {
                    for (int i = 0; i < context.AllSlots.Count; i++)
                    {
                        var slot = context.AllSlots[i];
                        if (!IsEligible(slot, inventory))
                            continue;

                        if (!result.Contains(slot))
                            result.Add(slot);
                    }
                }
            }

            if (result.Count == 0 && _fallbackToActiveSlotIfSelectionEmpty && IsEligible(activeSlot, inventory))
                result.Add(activeSlot);

            return result;
        }

        private bool IsEligible(ISlot slot, UniversalInventory inventory)
        {
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return false;

            if (_restrictToSameInventory && inventory != null && !ReferenceEquals(slot.Inventory, inventory))
                return false;

            return true;
        }
    }
}
