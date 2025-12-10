using System;
using System.Collections.Generic;
using Plugins.DragAndDropSystem.Examples;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
{
    /// <summary>
    /// Компонент для хранения данных инвентаря игрока.
    /// Не содержит UI логики, только данные и события.
    /// </summary>
    public class PlayerInventoryData : MonoBehaviour
    {
        [Header("Inventory Configuration")]
        [SerializeField, Tooltip("Предметы в инвентаре игрока")]
        private List<ItemExampleSO> _items = new();

        // Events
        public event Action OnInventoryChanged;

        // Properties
        public List<ItemExampleSO> Items => _items;
        public int ItemCount => _items.Count;
        public bool IsEmpty => _items.Count == 0;

        /// <summary>
        /// Добавить предмет в инвентарь
        /// </summary>
        public bool AddItem(ItemExampleSO item)
        {
            if (item == null)
            {
                Debug.LogWarning("[PlayerInventoryData] Trying to add null item");
                return false;
            }

            _items.Add(item);
            OnInventoryChanged?.Invoke();

            Debug.Log($"[PlayerInventoryData] Added '{item.ItemName}' to inventory.");
            return true;
        }

        /// <summary>
        /// Убрать предмет из инвентаря
        /// </summary>
        public bool RemoveItem(ItemExampleSO item)
        {
            if (item == null)
            {
                Debug.LogWarning("[PlayerInventoryData] Trying to remove null item");
                return false;
            }

            bool removed = _items.Remove(item);

            if (removed)
            {
                OnInventoryChanged?.Invoke();
                Debug.Log($"[PlayerInventoryData] Removed '{item.ItemName}' from inventory.");
            }
            else
            {
                Debug.LogWarning($"[PlayerInventoryData] Item '{item.ItemName}' not found in inventory");
            }

            return removed;
        }
    }
}
