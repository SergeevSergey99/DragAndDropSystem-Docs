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
    /// Сохраняет позиции предметов в слотах.
    /// </summary>
    public class PlayerInventoryDataBinding : InventoryDataBindingBase
    {
        [Header("Player Data")]
        [SerializeField, Tooltip("Компонент с данными инвентаря игрока")]
        private PlayerInventoryData _playerData;

        /// <summary>
        /// Синхронизация: данные игрока → UI
        /// Восстанавливает предметы в тех же слотах, где они были
        /// </summary>
        public override void ReloadUI()
        {
            _isSyncing = true;
            _inventory.ClearAll();

            // Загружаем предметы в соответствующие слоты
            var slots = _playerData.Slots;
            int loadedCount = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                var itemSO = slots[i];
                if (itemSO == null)
                    continue;

                // Создаем адаптер для предмета
                IInventoryItem itemAdapter = CreateAdapter(itemSO);

                // Добавляем в конкретный слот UI
                _inventory.TryAddItem(itemAdapter, 1, i);
                loadedCount++;
            }

            Debug.Log($"[PlayerInventoryDataBinding] Loaded {loadedCount} items from player data to UI");

            _isSyncing = false;
        }

        /// <summary>
        /// UI изменился: предмет добавлен → обновить данные игрока
        /// Сохраняет предмет в тот же слот в данных
        /// </summary>
        protected override void OnItemAddedToUI(InventoryItemEventArgs args)
        {
            var itemSO = ExtractItemSO(args.Item);
            if (itemSO == null)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Cannot extract ItemSO from {args.Item.GetType().Name}");
                return;
            }

            // Используем индекс слота из UI
            int slotIndex = args.TargetSlot?.Index ?? -1;
            if (slotIndex < 0)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] No target slot index for '{itemSO.ItemName}'");
                return;
            }

            bool added = _playerData.SetItem(slotIndex, itemSO);

            if (!added)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Failed to set '{itemSO.ItemName}' to slot {slotIndex}");
            }
        }

        /// <summary>
        /// UI изменился: предмет убран → обновить данные игрока
        /// Очищает соответствующий слот в данных
        /// </summary>
        protected override void OnItemRemovedFromUI(InventoryItemEventArgs args)
        {
            // Используем индекс исходного слота
            int slotIndex = args.SourceSlot?.Index ?? -1;
            if (slotIndex < 0)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] No source slot index for removed item");
                return;
            }

            bool cleared = _playerData.ClearSlot(slotIndex);

            if (!cleared)
            {
                Debug.LogWarning($"[PlayerInventoryDataBinding] Failed to clear slot {slotIndex}");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Создать адаптер для ItemSO
        /// </summary>
        private IInventoryItem CreateAdapter(ItemExampleSO itemSO)
        {
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
