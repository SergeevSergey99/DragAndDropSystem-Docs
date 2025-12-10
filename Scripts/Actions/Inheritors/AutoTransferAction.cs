using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
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

        public override string DisplayName => "Auto Transfer";

        public override bool Execute(UniversalInventory inventory, UniversalSlot activeSlot, bool logWarnings)
        {
            var dragManager = DragAndDropManager.Instance;
            if (dragManager == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"AutoTransferAction: DragAndDropManager not found.");
                }
                return false;
            }

            if (dragManager.IsDragging)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"AutoTransferAction: Cannot execute while dragging.");
                }
                return false;
            }

            if (activeSlot == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"AutoTransferAction: Active slot not found.");
                }
                return false;
            }

            if (activeSlot.IsEmpty)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"AutoTransferAction: Slot '{activeSlot.name}' is empty.");
                }
                return false;
            }

            var targets = ResolveTargets(inventory);
            if (targets.Count == 0)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"AutoTransferAction: No target inventories configured.");
                }
                return false;
            }

            foreach (var targetInventory in targets)
            {
                if (ReferenceEquals(targetInventory, inventory))
                    continue;

                if (targetInventory == null || !targetInventory.isActiveAndEnabled)
                    continue;

                var success = dragManager.TryAutoTransfer(
                    activeSlot,
                    inventory,
                    targetInventory);

                if (success)
                {
                    inventory.NotifySlotInteracted(activeSlot);
                    return true;
                }
            }

            if (logWarnings)
            {
                Debug.LogWarning($"AutoTransferAction: Failed to transfer to any target inventory.");
            }

            return false;
        }

        public override bool CanExecute(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            if (!base.CanExecute(inventory, activeSlot))
                return false;

            if (activeSlot == null || activeSlot.IsEmpty)
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
    }
}
