using System;
using System.Collections.Generic;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo2Loot
{
    /// <summary>
    /// Сундук - контейнер с предметами.
    /// НЕ знает о UI, только хранит данные и вызывает события.
    /// </summary>
    public class Chest : MonoBehaviour, IInteractable
    {
        [Header("Loot Configuration")]
        [SerializeField, Tooltip("Содержимое сундука")]
        private List<ItemExampleWith3DSO> _items = new();

        private bool _isOpen = false;

        public event Action<Chest> OnChestOpened;
        public event Action<Chest> OnChestClosed;
        public event Action<Chest> OnChestEmptied;

        public bool IsOpen => _isOpen;
        public bool IsEmpty => _items.Count == 0;
        
        public List<ItemExampleWith3DSO> GetItems() => _items;

        private void Awake()
        {
            // Проверяем пустой ли сундук
            CheckIfEmpty();
        }

        public bool CanInteract(PlayerInteraction player) => true;
        
        public void Interact(PlayerInteraction player)
        {
            // Переключаем состояние
            _isOpen = !_isOpen;

            // Вызываем события (UI подпишется и покажет/скроет окно)
            if (_isOpen)
            {
                OnChestOpened?.Invoke(this);
                Debug.Log($"[Chest] Opened chest '{gameObject.name}' with {_items.Count} items");
            }
            else
            {
                OnChestClosed?.Invoke(this);
                Debug.Log($"[Chest] Closed chest '{gameObject.name}'");
            }
        }

        public bool AddItem(ItemExampleWith3DSO item)
        {
            if (item == null) return false;
            
            _items.Add(item);
            Debug.Log($"[Chest] Added '{item.ItemName}' to chest. Total items: {_items.Count}");

            CheckIfEmpty();
            return true;
        }
        
        public bool RemoveItem(ItemExampleWith3DSO item)
        {
            if (item == null)
            {
                Debug.LogWarning("[Chest] Trying to remove null itemAdapter");
                return false;
            }

            bool removed = _items.Remove(item);

            if (removed)
            {
                Debug.Log($"[Chest] Removed '{item.ItemName}' from chest. Remaining items: {_items.Count}");
                CheckIfEmpty();
            }
            else
            {
                Debug.LogWarning($"[Chest] PrimaryAdapter '{item.ItemName}' not found in chest");
            }

            return removed;
        }

        private void CheckIfEmpty()
        {
            bool wasEmpty = _items.Count == 0;

            // Если только что опустел
            if (IsEmpty && !wasEmpty)
            {
                OnChestEmptied?.Invoke(this);
                Debug.Log("[Chest] Chest is now empty");
            }
        }
    }
}
