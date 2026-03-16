using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
{
    /// <summary>
    /// DataBinding для инвентаря сундука.
    /// Связывает Chest (данные) ↔ UniversalInventory (UI).
    /// Привязывается динамически через BindToChest().
    /// </summary>
    public class ChestInventoryDataBinding : InventoryDataBindingBase
    {
        private Chest _chest;

        /// <summary>
        /// Привязать этот биндинг к конкретному сундуку.
        /// Вызывается из LootUIController когда открывается сундук.
        /// </summary>
        public void BindToChest(Chest chest)
        {
            // Отвязываемся от старого сундука если был
            if (_chest != null)
            {
                Debug.Log($"[ChestInventoryDataBinding] Unbinding from chest");
            }

            _chest = chest;

            if (_chest != null)
            {
                Debug.Log($"[ChestInventoryDataBinding] Bound to chest '{_chest.gameObject.name}'");
                ReloadUI(); // Синхронизируем UI с данными нового сундука
            }
            else
            {
                // Если передали null, очищаем UI
                ClearUI();
            }
        }

        /// <summary>
        /// Синхронизация: данные сундука → UI
        /// Вызывается когда открывается сундук или меняются его данные извне
        /// </summary>
        protected override void OnReloadUI()
        {
            if (_chest == null)
                return;

            Debug.Log($"[ChestInventoryDataBinding] Syncing chest '{_chest.gameObject.name}' to UI");

            var items = _chest.GetItems();
            if (items == null || items.Count == 0)
                return;

            foreach (var itemSO in items)
            {
                if (itemSO == null)
                    continue;

                IInventoryItem itemAdapter = new ItemSOWith3DAdapter(itemSO);
                AddToUIQuiet(itemAdapter, 1);
            }

            Debug.Log($"[ChestInventoryDataBinding] Loaded {items.Count} items from chest to UI");
        }

        /// <summary>
        /// UI изменился: предмет добавлен → обновить данные сундука
        /// Вызывается когда предмет перетащили В этот инвентарь
        /// </summary>
        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (_chest == null)
            {
                Debug.LogWarning("[ChestInventoryDataBinding] Cannot add item - chest is null");
                return;
            }

            // Извлекаем ItemSO из адаптера
            var itemSO = ExtractItemSO(context.Item);
            if (itemSO == null)
            {
                Debug.LogWarning($"[ChestInventoryDataBinding] Cannot extract ItemSO from {context.Item.GetType().Name}");
                return;
            }

            // Добавляем в данные сундука
            bool added = _chest.AddItem(itemSO);

            if (!added)
            {
                Debug.LogWarning($"[ChestInventoryDataBinding] Failed to add '{itemSO.ItemName}' to chest data");
            }
        }

        /// <summary>
        /// UI изменился: предмет убран → обновить данные сундука
        /// Вызывается когда предмет перетащили ИЗ этого инвентаря
        /// </summary>
        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (_chest == null)
            {
                Debug.LogWarning("[ChestInventoryDataBinding] Cannot remove item - chest is null");
                return;
            }

            // Извлекаем ItemSO из адаптера
            var itemSO = ExtractItemSO(context.Item);
            if (itemSO == null)
            {
                Debug.LogWarning($"[ChestInventoryDataBinding] Cannot extract ItemSO from {context.Item.GetType().Name}");
                return;
            }

            // Убираем из данных сундука
            bool removed = _chest.RemoveItem(itemSO);

            if (!removed)
            {
                Debug.LogWarning($"[ChestInventoryDataBinding] Failed to remove '{itemSO.ItemName}' from chest data");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Извлечь ItemSO из адаптера
        /// </summary>
        private ItemExampleWith3DSO ExtractItemSO(IInventoryItem item)
        {
            if (item is ItemSOWith3DAdapter adapter3D)
            {
                return adapter3D.item;
            }

            return null;
        }

        #endregion
    }
}
