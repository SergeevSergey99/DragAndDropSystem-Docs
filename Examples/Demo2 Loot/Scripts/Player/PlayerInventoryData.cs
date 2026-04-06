using System;
using System.Collections.Generic;
using System.Linq;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// Компонент для хранения данных инвентаря игрока.
    /// Использует фиксированное количество слотов с null для пустых.
    /// Не содержит UI логики, только данные и события.
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
        /// Список слотов (null = пустой слот). Длина всегда равна SlotCount.
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
        /// Гарантирует что список слотов имеет правильный размер
        /// </summary>
        private void EnsureSlotCount()
        {
            while (_slots.Count < _slotCount)
                _slots.Add(null);
            while (_slots.Count > _slotCount)
                _slots.RemoveAt(_slots.Count - 1);
        }

        /// <summary>
        /// Добавить предмет в конкретный слот
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
        /// Получить предмет из слота
        /// </summary>
        public ItemExampleWith3DSO GetItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount)
                return null;

            return _slots[slotIndex];
        }

        /// <summary>
        /// Очистить слот
        /// </summary>
        public bool ClearSlot(int slotIndex)
        {
            return SetItem(slotIndex, null);
        }

        /// <summary>
        /// Найти первый пустой слот
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
        /// Добавить предмет в первый свободный слот
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
        /// Убрать предмет из инвентаря (ищет по ссылке)
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
