using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Tools;
using UnityEngine;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// ПРИМЕР расширения GameManager с методами и событиями для Data Binding
    /// Скопируй эти методы в свой GameManager или адаптируй под свою структуру
    /// </summary>
    public class GameManagerExample : MonoBehaviour
    {
        // Твои существующие списки
        private List<ItemData> _itemsInInventory = new List<ItemData>();
        private List<ItemData> _itemsOnCraftTable = new List<ItemData>();

        // События для Data Binding
        public event Action OnInventoryChanged;
        public event Action OnCraftTableChanged;

        #region Player Inventory Methods

        /// <summary>
        /// Добавить предмет в инвентарь игрока
        /// </summary>
        public void AddToInventory(IInventoryItem item, int count)
        {
            Extentions.DragAndDropLog($"[GameManager] AddToInventory: {item.DisplayName} x{count}");

            // Ищем существующий предмет
            var existing = _itemsInInventory.Find(x => x.ItemId == item.ItemId);

            if (existing != null)
            {
                // Увеличиваем количество
                existing.Count += count;
            }
            else
            {
                // Добавляем новый
                _itemsInInventory.Add(new ItemData(item, count));
            }

            // Вызываем событие
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Удалить предмет из инвентаря игрока
        /// </summary>
        public void RemoveFromInventory(IInventoryItem item, int count)
        {
            Extentions.DragAndDropLog($"[GameManager] RemoveFromInventory: {item.DisplayName} x{count}");

            var existing = _itemsInInventory.Find(x => x.ItemId == item.ItemId);

            if (existing != null)
            {
                existing.Count -= count;

                // Если количество <= 0, удаляем из списка
                if (existing.Count <= 0)
                {
                    _itemsInInventory.Remove(existing);
                }
            }

            // Вызываем событие
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Получить все предметы в инвентаре
        /// </summary>
        public List<ItemData> GetInventoryItems()
        {
            return _itemsInInventory;
        }

        /// <summary>
        /// Очистить инвентарь
        /// </summary>
        public void ClearInventory()
        {
            _itemsInInventory.Clear();
            OnInventoryChanged?.Invoke();
        }

        #endregion

        #region Craft Table Methods

        /// <summary>
        /// Добавить предмет на крафт-стол
        /// </summary>
        public void AddToCraftTable(IInventoryItem item, int count)
        {
            Extentions.DragAndDropLog($"[GameManager] AddToCraftTable: {item.DisplayName} x{count}");

            // Ищем существующий предмет
            var existing = _itemsOnCraftTable.Find(x => x.ItemId == item.ItemId);

            if (existing != null)
            {
                // Увеличиваем количество
                existing.Count += count;
            }
            else
            {
                // Добавляем новый
                _itemsOnCraftTable.Add(new ItemData(item, count));
            }

            // Вызываем событие
            OnCraftTableChanged?.Invoke();
        }

        /// <summary>
        /// Удалить предмет с крафт-стола
        /// </summary>
        public void RemoveFromCraftTable(IInventoryItem item, int count)
        {
            Extentions.DragAndDropLog($"[GameManager] RemoveFromCraftTable: {item.DisplayName} x{count}");

            var existing = _itemsOnCraftTable.Find(x => x.ItemId == item.ItemId);

            if (existing != null)
            {
                existing.Count -= count;

                // Если количество <= 0, удаляем из списка
                if (existing.Count <= 0)
                {
                    _itemsOnCraftTable.Remove(existing);
                }
            }

            // Вызываем событие
            OnCraftTableChanged?.Invoke();
        }

        /// <summary>
        /// Получить все предметы на крафт-столе
        /// </summary>
        public List<ItemData> GetCraftTableItems()
        {
            return _itemsOnCraftTable;
        }

        /// <summary>
        /// Очистить крафт-стол
        /// </summary>
        public void ClearCraftTable()
        {
            _itemsOnCraftTable.Clear();
            OnCraftTableChanged?.Invoke();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Проверить, есть ли предмет в инвентаре
        /// </summary>
        public bool HasItemInInventory(string itemId, int minCount = 1)
        {
            var item = _itemsInInventory.Find(x => x.ItemId == itemId);
            return item != null && item.Count >= minCount;
        }

        /// <summary>
        /// Получить количество предмета в инвентаре
        /// </summary>
        public int GetItemCountInInventory(string itemId)
        {
            var item = _itemsInInventory.Find(x => x.ItemId == itemId);
            return item?.Count ?? 0;
        }

        #endregion
    }

    /// <summary>
    /// Класс-обертка для хранения данных предмета
    /// Адаптируй под свою структуру
    /// </summary>
    [System.Serializable]
    public class ItemData
    {
        public string ItemId;
        public int Count;

        // Опционально: ссылка на ItemSO или IInventoryItem
        // public ItemSO ItemSO;
        // public IInventoryItem Item;

        public ItemData(IInventoryItem item, int count)
        {
            ItemId = item.ItemId;
            Count = count;
            // ItemSO = item as ItemSO;
        }

        public ItemData(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
