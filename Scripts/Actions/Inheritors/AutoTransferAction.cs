using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Interaction;
using UDND.Selection;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.Inventories
{
    /// <summary>
    /// Action that automatically transfers an item from the active slot into target inventories
    /// </summary>
    [Serializable]
    public class AutoTransferAction : InventoryActionBase
    {
        [SerializeField, Tooltip("Target inventories for auto-transfer")]
        [HideLabel]
        private InventoryList _targetInventories = new InventoryList();

        [SerializeField, Tooltip("Use the current selection for multi auto-transfer")]
        private bool _useSelectionForBatch = true;

        public override string DisplayName => "Auto Transfer";

        public override ActionResult Execute(IInventory inventory, BaseSlot activeBaseSlot)
        {
            var dragManager = DragAndDropManager.AutoCreateInstance;
            if (dragManager == null || dragManager.IsDragging)
                return ActionResult.Failed("Invalid drag manager state");
            
            var sourceSlots = ResolveSourceSlots(inventory);
            if (sourceSlots.Count == 0)
                return ActionResult.Failed("No valid source slots for auto transfer");

            var targets = ResolveTargets(inventory);
            if (targets.Count == 0)
                return ActionResult.Failed("No target inventories configured");
            
            foreach (var targetInventory in targets)
            {
                if (ReferenceEquals(targetInventory, inventory))
                    continue;

                if (targetInventory == null || !IsInventoryActiveAndEnabled(targetInventory))
                    continue;

                var success = dragManager.TryAutoTransfer(
                    sourceSlots,
                    inventory,
                    targetInventory);

                if (success)
                {
                    if (activeBaseSlot != null)
                        NotifySlotInteracted(inventory, activeBaseSlot);
                    
                    if (SelectionManager.IsInstanceExist)
                        SelectionManager.AutoCreateInstance.Clear();
                    return ActionResult.Succeeded();
                }
            }

            return ActionResult.Failed("Auto transfer failed for all targets");
        }

        public override bool CanExecute(IInventory inventory, BaseSlot activeBaseSlot)
        {
            if (!base.CanExecute(inventory, activeBaseSlot))
                return false;

            if (InputEventRouter.IsInstanceExist && !InputEventRouter.AutoCreateInstance.IsInventoryActive(inventory))
                return false;

            bool hasSelectionSources = HasSelectionSources(inventory);
            bool hasContextSlot = ResolveContextSourceSlot(inventory) != null;
            if (!hasContextSlot && !hasSelectionSources)
                return false;

            var dragManager = DragAndDropManager.AutoCreateInstance;
            if (dragManager == null || dragManager.IsDragging)
                return false;

            var targets = ResolveTargets(inventory);
            return targets.Count > 0;
        }

        private List<IInventory> ResolveTargets(IInventory owner)
        {
            var result = new List<IInventory>();

            if (_targetInventories != null && _targetInventories.inventories != null)
            {
                foreach (var target in _targetInventories.inventories)
                {
                    if (target != null && !result.Contains(target))
                    {
                        result.Add(target);
                    }
                }
            }

            return result;
        }

        private List<BaseSlot> ResolveSourceSlots(IInventory inventory)
        {
            var result = new List<BaseSlot>();

            if (_useSelectionForBatch && SelectionManager.IsInstanceExist)
            {
                var context = SelectionManager.AutoCreateInstance.CurrentContext;
                if (context != null && context.HasSelection)
                {
                    for (int i = 0; i < context.AllSlots.Count; i++)
                    {
                        var slot = context.AllSlots[i];
                        if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                            continue;

                        if (!ReferenceEquals(slot.Inventory, inventory))
                            continue;

                        if (!result.Contains(slot))
                            result.Add(slot);
                    }
                }
            }

            if (result.Count == 0)
            {
                var contextSlot = ResolveContextSourceSlot(inventory);
                if (contextSlot != null)
                    result.Add(contextSlot);
            }

            return result;
        }

        private bool HasSelectionSources(IInventory inventory)
        {
            if (!_useSelectionForBatch || !SelectionManager.IsInstanceExist)
                return false;

            var context = SelectionManager.AutoCreateInstance.CurrentContext;
            if (context == null || !context.HasSelection)
                return false;

            for (int i = 0; i < context.AllSlots.Count; i++)
            {
                var slot = context.AllSlots[i];
                if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                    continue;

                if (ReferenceEquals(slot.Inventory, inventory))
                    return true;
            }

            return false;
        }

        private BaseSlot ResolveContextSourceSlot(IInventory inventory)
        {
            var contextSlot = InputEventRouter.IsInstanceExist
                ? InputEventRouter.AutoCreateInstance.ResolveQuickActionSlot(inventory, requireActiveInventory: true)
                : null;

            if (contextSlot != null && !contextSlot.IsEmpty && contextSlot.IsInteractable)
                return contextSlot;

            return null;
        }

        private static void NotifySlotInteracted(IInventory inventory, BaseSlot baseSlot)
        {
            if (inventory is IInventoryInteractionSurface interactionSurface)
                interactionSurface.NotifySlotInteracted(baseSlot);
        }

        private static bool IsInventoryActiveAndEnabled(IInventory inventory)
        {
            return inventory is MonoBehaviour monoBehaviour && monoBehaviour.isActiveAndEnabled;
        }
    }
}
