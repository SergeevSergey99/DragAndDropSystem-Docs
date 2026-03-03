using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Действие автоматического переноса предмета из активного слота в целевые инвентари
    /// </summary>
    [Serializable]
    public class AutoTransferAction : InventoryActionBase
    {
        [SerializeField, Tooltip("Целевые инвентари для автопереноса")]
        [HideLabel]
        private InventoryList _targetInventories = new InventoryList();

        [SerializeField, Tooltip("Использовать текущее выделение для множественного автопереноса")]
        private bool _useSelectionForBatch = true;

        public override string DisplayName => "Auto Transfer";

        public override ActionResult Execute(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            var dragManager = DragAndDropManager.Instance;
            if (dragManager == null || dragManager.IsDragging)
                return ActionResult.Failed("Invalid drag manager state");
            
            var sourceSlots = ResolveSourceSlots(inventory, activeSlot);
            if (sourceSlots.Count == 0)
                return ActionResult.Failed("No valid source slots for auto transfer");

            var targets = ResolveTargets(inventory);
            if (targets.Count == 0)
                return ActionResult.Failed("No target inventories configured");
            
            foreach (var targetInventory in targets)
            {
                if (ReferenceEquals(targetInventory, inventory))
                    continue;

                if (targetInventory == null || !targetInventory.isActiveAndEnabled)
                    continue;

                var success = dragManager.TryAutoTransfer(
                    sourceSlots,
                    inventory,
                    targetInventory);

                if (success)
                {
                    if (activeSlot != null)
                        inventory.NotifySlotInteracted(activeSlot);
                    return ActionResult.Succeeded();
                }
            }

            return ActionResult.Failed("Auto transfer failed for all targets");
        }

        public override bool CanExecute(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            if (!base.CanExecute(inventory, activeSlot))
                return false;

            bool hasActiveSlot = activeSlot != null && !activeSlot.IsEmpty && activeSlot.IsInteractable;
            bool hasSelectionSources = _useSelectionForBatch && ResolveSourceSlots(inventory, activeSlot).Count > 0;
            if (!hasActiveSlot && !hasSelectionSources)
                return false;

            var dragManager = DragAndDropManager.Instance;
            if (dragManager == null || dragManager.IsDragging)
                return false;

            var targets = ResolveTargets(inventory);
            return targets.Count > 0;
        }

        private List<UniversalInventory> ResolveTargets(UniversalInventory owner)
        {
            var result = new List<UniversalInventory>();

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

        private List<ISlot> ResolveSourceSlots(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            var result = new List<ISlot>();

            if (_useSelectionForBatch && SelectionManager.IsInstanceExist)
            {
                var context = SelectionManager.Instance.CurrentContext;
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

            if (result.Count == 0 && activeSlot != null && !activeSlot.IsEmpty && activeSlot.IsInteractable)
                result.Add(activeSlot);

            return result;
        }
    }
}
