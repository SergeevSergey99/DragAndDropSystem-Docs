using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
{
    /// <summary>
    /// DataBinding для инвентаря игрока.
    /// Связывает PlayerInventoryData (данные) ↔ UniversalInventory (UI).
    /// Привязывается статически в Awake и остается активным всегда.
    /// </summary>
    public class PlayerInventoryDataBinding : InventoryDataBindingBase
    {
        [Header("Player Data")]
        [SerializeField, Tooltip("Компонент с данными инвентаря игрока")]
        private PlayerInventoryData _playerData;

        /// <summary>
        /// Синхронизация: данные игрока → UI
        /// Вызывается при старте или когда данные меняются извне
        /// </summary>
        public override void ReloadUI()
        {
            // Очищаем UI
            _isSyncing = true;
            _inventory.ClearAll();

            // Загружаем предметы из данных игрока
            var items = _playerData.Items;
            if (items != null && items.Count > 0)
            {
                foreach (var itemSO in items)
                {
                    if (itemSO == null)
                        continue;

                    // Создаем адаптер для предмета
                    IInventoryItem itemAdapter = CreateAdapter(itemSO);

                    // Добавляем в UI
                    AddToUIQuiet(itemAdapter, 1);
                }

                Debug.Log($"[PlayerInventoryDataBinding] Loaded {items.Count} items from player data to UI");
            }

            _isSyncing = false;
        }

        /// <summary>
        /// UI изменился: предмет добавлен → обновить данные игрока
        /// Вызывается когда предмет перетащили В инвентарь игрока
        /// </summary>
        protected override void OnItemAddedToUI(InventoryItemEventArgs args)
        {
            // Извлекаем ItemSO из адаптера
            var itemSO = ExtractItemSO(args.Item);
            if (itemSO == null)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Cannot extract ItemSO from {args.Item.GetType().Name}");
                return;
            }

            // Добавляем в данные игрока
            bool added = _playerData.AddItem(itemSO);

            if (!added)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Failed to add '{itemSO.ItemName}' to player data");
            }
        }

        /// <summary>
        /// UI изменился: предмет убран → обновить данные игрока
        /// Вызывается когда предмет перетащили ИЗ инвентаря игрока
        /// </summary>
        protected override void OnItemRemovedFromUI(InventoryItemEventArgs args)
        {
            // Извлекаем ItemSO из адаптера
            var itemSO = ExtractItemSO(args.Item);
            if (itemSO == null)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Cannot extract ItemSO from {args.Item.GetType().Name}");
                return;
            }

            // Убираем из данных игрока
            bool removed = _playerData.RemoveItem(itemSO);

            if (!removed)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Failed to remove '{itemSO.ItemName}' from player data");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Создать адаптер для ItemSO
        /// </summary>
        private IInventoryItem CreateAdapter(ItemExampleSO itemSO)
        {
            // Проверяем тип и создаем соответствующий адаптер
            if (itemSO is ItemExampleWith3DSO item3D)
            {
                return new ItemSOWith3DAdapter(item3D);
            }
            else
            {
                return new ItemSOAdapter(itemSO);
            }
        }

        /// <summary>
        /// Извлечь ItemSO из адаптера
        /// </summary>
        private ItemExampleSO ExtractItemSO(IInventoryItem item)
        {
            if (item is ItemSOWith3DAdapter adapter3D)
            {
                return adapter3D.item;
            }
            else if (item is ItemSOAdapter adapter)
            {
                return adapter.item;
            }

            return null;
        }

        #endregion
    }
}
