using System;
using System.Collections.Generic;
using System.Linq;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// Component for storing player inventory data.
    /// Uses a fixed number of slots with null for empty entries.
    /// Contains no UI logic, only data and events.
    /// </summary>
    public class PlayerInventoryData : MonoBehaviour
    {
        [Header("Inventory Configuration")]
        [SerializeField, Tooltip("Number of inventory slots")]
        private int _slotCount = 9;

        [SerializeField, Tooltip("Items in the player's inventory (null = empty slot)")]
        private List<ItemExampleWith3DSO> _slots = new();

        // Events
        public event Action OnInventoryChanged;

        // Properties
        /// <summary>
        /// List of slots (null = empty slot). Length always equals SlotCount.
        /// </summary>
        public IReadOnlyList<ItemExampleWith3DSO> Slots => _slots;
        public int SlotCount => _slotCount;
        public int ItemCount => _slots.Count(s => s != null);
        public bool IsEmpty => _slots.All(s => s == null);
        public bool IsFull => _slots.All(s => s != null);

        private void Awake()
        {
            EnsureSlotCount();
        }

        private void OnValidate()
        {
            EnsureSlotCount();
        }

        /// <summary>
        /// Ensures the slot list has the correct size
        /// </summary>
        private void EnsureSlotCount()
        {
            while (_slots.Count < _slotCount)
                _slots.Add(null);
            while (_slots.Count > _slotCount)
                _slots.RemoveAt(_slots.Count - 1);
        }

        /// <summary>
        /// Add an item to a specific slot
        /// </summary>
        public bool SetItem(int slotIndex, ItemExampleWith3DSO item)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount)
            {
                Debug.LogWarning($"[PlayerInventoryData] Invalid slot index: {slotIndex}");
                return false;
            }

            _slots[slotIndex] = item;
            OnInventoryChanged?.Invoke();

            if (item != null)
                Debug.Log($"[PlayerInventoryData] Set '{item.ItemName}' to slot {slotIndex}");
            else
                Debug.Log($"[PlayerInventoryData] Cleared slot {slotIndex}");

            return true;
        }

        /// <summary>
        /// Get an item from a slot
        /// </summary>
        public ItemExampleWith3DSO GetItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount)
                return null;

            return _slots[slotIndex];
        }

        /// <summary>
        /// Clear a slot
        /// </summary>
        public bool ClearSlot(int slotIndex)
        {
            return SetItem(slotIndex, null);
        }

        /// <summary>
        /// Find the first empty slot
        /// </summary>
        public int FindEmptySlot()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Add an item to the first free slot
        /// </summary>
        public bool AddItem(ItemExampleWith3DSO item)
        {
            if (item == null)
            {
                Debug.LogWarning("[PlayerInventoryData] Trying to add null itemAdapter");
                return false;
            }

            int emptySlot = FindEmptySlot();
            if (emptySlot < 0)
            {
                Debug.LogWarning("[PlayerInventoryData] No empty slots available");
                return false;
            }

            return SetItem(emptySlot, item);
        }

        /// <summary>
        /// Remove an item from the inventory (searches by reference)
        /// </summary>
        public bool RemoveItem(ItemExampleWith3DSO item)
        {
            if (item == null)
            {
                Debug.LogWarning("[PlayerInventoryData] Trying to remove null itemAdapter");
                return false;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == item)
                {
                    return ClearSlot(i);
                }
            }

            Debug.LogWarning($"[PlayerInventoryData] _PrimaryAdapter '{item.ItemName}' not found in inventory");
            return false;
        }
    }
}
