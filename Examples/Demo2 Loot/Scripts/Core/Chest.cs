using System;
using System.Collections.Generic;
using UDND.Examples;
using UnityEngine;

namespace UDND.Examples.Loot
{
    /// <summary>
    /// Chest: a container with items.
    /// Does NOT know about the UI; it only stores data and raises events.
    /// </summary>
    public class Chest : MonoBehaviour, IInteractable
    {
        [Header("Loot Configuration")]
        [SerializeField, Tooltip("Chest contents")]
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
            // Check whether the chest is empty
            CheckIfEmpty();
        }

        public bool CanInteract(PlayerInteraction player) => true;
        
        public void Interact(PlayerInteraction player)
        {
            // Toggle state
            _isOpen = !_isOpen;

            // Raise events (the UI will subscribe and show/hide the window)
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

            // If it just became empty
            if (IsEmpty && !wasEmpty)
            {
                OnChestEmptied?.Invoke(this);
                Debug.Log("[Chest] Chest is now empty");
            }
        }
    }
}