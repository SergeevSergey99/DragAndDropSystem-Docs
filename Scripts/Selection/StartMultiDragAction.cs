using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Interaction;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Selection
{
    [Serializable]
    public sealed class StartMultiDragAction : AssetSafeSlotInteractionAction
    {
        [SerializeField] private bool _restrictToSameInventory = true;
        [SerializeField, Tooltip("Temporary item amount override for the current StartDrag. Applied to each selected source slot.")]
        private DragRequestPolicySettings _dragPolicyOverride = new DragRequestPolicySettings();
        
        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
        {
            if (snapshot?.Inventory == null || DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var sourceSlots = BuildSourceSlots(snapshot.Inventory, snapshot.ActiveSlot);
            return sourceSlots.Count > 0;
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            var inventory = snapshot?.Inventory;
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Already dragging");

            var sourceSlots = BuildSourceSlots(inventory, snapshot?.ActiveSlot);
            if (sourceSlots.Count == 0)
                return ActionResult.Failed("No valid slots for multi drag");

            return DragAndDropManager.AutoCreateInstance.StartDrag(sourceSlots, _dragPolicyOverride.TryBuild())
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Failed to start multi drag");
        }

        private List<BaseSlot> BuildSourceSlots(IInventory inventory, BaseSlot activeSlot)
        {
            var result = new List<BaseSlot>();

            if (SelectionManager.IsInstanceExist)
            {
                var context = SelectionManager.AutoCreateInstance.CurrentContext;
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

            if (!result.Contains(activeSlot) && IsEligible(activeSlot, inventory))
                result.Add(activeSlot);

            return result;
        }

        private bool IsEligible(BaseSlot baseSlot, IInventory inventory)
        {
            if (baseSlot == null || baseSlot.IsEmpty || !baseSlot.IsInteractable)
                return false;

            if (_restrictToSameInventory && inventory != null && !ReferenceEquals(baseSlot.Inventory, inventory))
                return false;

            return true;
        }
    }
}
