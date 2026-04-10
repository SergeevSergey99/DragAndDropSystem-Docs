using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// DataBinding for the player inventory.
    /// Connects PlayerInventoryData (data) with UniversalInventory (UI).
    /// Preserves item positions in slots.
    /// </summary>
    public class PlayerInventoryDataBinding : SlotIndexedInventoryDataBinding<ItemExampleWith3DSO, ItemAdapterSoWith3DAdapter>
    {
        [Header("Player Data")]
        [SerializeField, Tooltip("Player inventory data component")]
        private PlayerInventoryData _playerData;

        protected override IEnumerable<(int index, ItemExampleWith3DSO item, int count)> GetOccupiedSlots()
        {
            var slots = _playerData.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null)
                    yield return (i, slots[i], 1);
        }

        protected override ItemAdapterSoWith3DAdapter CreateAdapter(ItemExampleWith3DSO item) => new(item);
        protected override void AddToSlotData(int index, ItemAdapterSoWith3DAdapter adapter, int count) => _playerData.SetItem(index, adapter.item);
        protected override void RemoveFromSlotData(int index, ItemAdapterSoWith3DAdapter adapter, int count) => _playerData.ClearSlot(index);
        
        // Additionally forbid placing into an occupied slot in any inventory mode
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Get the slot index the item is being dropped onto
            int targetIndex = context.TargetBaseSlot.Index;

            // Check whether this slot is occupied
            if (_playerData.GetItem(targetIndex) != null)
            {
                return RuleResult.Failure("This slot is already occupied!");
            }

            // If the slot is free, allow the drop
            return RuleResult.Success();
        }
    }
}
