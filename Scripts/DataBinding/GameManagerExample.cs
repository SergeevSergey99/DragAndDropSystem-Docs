using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Tools;
using UnityEngine;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// EXAMPLE of extending GameManager with methods and events for Data Binding
    /// Copy these methods into your own GameManager or adapt them to your structure
    /// </summary>
    public class GameManagerExample : MonoBehaviour
    {
        // Your existing lists
        private List<ItemData> _itemsInInventory = new List<ItemData>();
        private List<ItemData> _itemsOnCraftTable = new List<ItemData>();

        // Events for Data Binding
        public event Action OnInventoryChanged;
        public event Action OnCraftTableChanged;

        #region Player Inventory Methods

        /// <summary>
        /// Add an item to the player inventory
        /// </summary>
        public void AddToInventory(IItemAdapter itemAdapter, int count)
        {
            Extensions.DragAndDropLog($"[GameManager] AddToInventory: {itemAdapter.DisplayName} x{count}");

            // Search for an existing item
            var existing = _itemsInInventory.Find(x => x.ItemId == itemAdapter.ItemId);

            if (existing != null)
            {
                // Increase the quantity
                existing.Count += count;
            }
            else
            {
                // Add a new entry
                _itemsInInventory.Add(new ItemData(itemAdapter, count));
            }

            // Raise the event
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Remove an item from the player inventory
        /// </summary>
        public void RemoveFromInventory(IItemAdapter itemAdapter, int count)
        {
            Extensions.DragAndDropLog($"[GameManager] RemoveFromInventory: {itemAdapter.DisplayName} x{count}");

            var existing = _itemsInInventory.Find(x => x.ItemId == itemAdapter.ItemId);

            if (existing != null)
            {
                existing.Count -= count;

                // If quantity <= 0, remove it from the list
                if (existing.Count <= 0)
                {
                    _itemsInInventory.Remove(existing);
                }
            }

            // Raise the event
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Get all items in the inventory
        /// </summary>
        public List<ItemData> GetInventoryItems()
        {
            return _itemsInInventory;
        }

        /// <summary>
        /// Clear the inventory
        /// </summary>
        public void ClearInventory()
        {
            _itemsInInventory.Clear();
            OnInventoryChanged?.Invoke();
        }

        #endregion

        #region Craft Table Methods

        /// <summary>
        /// Add an item to the crafting table
        /// </summary>
        public void AddToCraftTable(IItemAdapter itemAdapter, int count)
        {
            Extensions.DragAndDropLog($"[GameManager] AddToCraftTable: {itemAdapter.DisplayName} x{count}");

            // Search for an existing item
            var existing = _itemsOnCraftTable.Find(x => x.ItemId == itemAdapter.ItemId);

            if (existing != null)
            {
                // Increase the quantity
                existing.Count += count;
            }
            else
            {
                // Add a new entry
                _itemsOnCraftTable.Add(new ItemData(itemAdapter, count));
            }

            // Raise the event
            OnCraftTableChanged?.Invoke();
        }

        /// <summary>
        /// Remove an item from the crafting table
        /// </summary>
        public void RemoveFromCraftTable(IItemAdapter itemAdapter, int count)
        {
            Extensions.DragAndDropLog($"[GameManager] RemoveFromCraftTable: {itemAdapter.DisplayName} x{count}");

            var existing = _itemsOnCraftTable.Find(x => x.ItemId == itemAdapter.ItemId);

            if (existing != null)
            {
                existing.Count -= count;

                // If quantity <= 0, remove it from the list
                if (existing.Count <= 0)
                {
                    _itemsOnCraftTable.Remove(existing);
                }
            }

            // Raise the event
            OnCraftTableChanged?.Invoke();
        }

        /// <summary>
        /// Get all items on the crafting table
        /// </summary>
        public List<ItemData> GetCraftTableItems()
        {
            return _itemsOnCraftTable;
        }

        /// <summary>
        /// Clear the crafting table
        /// </summary>
        public void ClearCraftTable()
        {
            _itemsOnCraftTable.Clear();
            OnCraftTableChanged?.Invoke();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check whether the inventory contains an item
        /// </summary>
        public bool HasItemInInventory(string itemId, int minCount = 1)
        {
            var item = _itemsInInventory.Find(x => x.ItemId == itemId);
            return item != null && item.Count >= minCount;
        }

        /// <summary>
        /// Get the quantity of an item in the inventory
        /// </summary>
        public int GetItemCountInInventory(string itemId)
        {
            var item = _itemsInInventory.Find(x => x.ItemId == itemId);
            return item?.Count ?? 0;
        }

        #endregion
    }

    /// <summary>
    /// Wrapper class for storing item data
    /// Adapt it to your own structure
    /// </summary>
    [System.Serializable]
    public class ItemData
    {
        public string ItemId;
        public int Count;

        // Optional: reference to ItemSO or IItemAdapter
        // public ItemSO ItemSO;
        // public IItemAdapter _PrimaryAdapter;

        public ItemData(IItemAdapter itemAdapter, int count)
        {
            ItemId = itemAdapter.ItemId;
            Count = count;
            // ItemSO = itemAdapter as ItemSO;
        }

        public ItemData(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}