using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Rules;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples.DataBindings
{
    /// <summary>
    /// Универсальный DataBinding для ItemExampleSO и ItemExampleWith3DSO
    /// Автоматически определяет нужный адаптер
    ///
    /// ПРИМЕР: Демонстрирует использование правил инвентаря и переопределение методов проверки переноса
    /// </summary>
    public class ItemsSOInventoryDataBinding : InventoryDataBindingBase
    {
        [FoldoutGroup("Data")]
        [SerializeField]
        [OnValueChanged(nameof(ReloadUI))]
        [InfoBox("Поддерживает как ItemExampleSO, так и ItemExampleWith3DSO (с 3D префабом)")]
        private List<ItemExampleSO> items;

        [FoldoutGroup("Custom Validation (Example)", expanded: false)]
        [SerializeField, Tooltip("Пример: запретить перетаскивание из этого инвентаря")]
        private bool _preventDragFromInventory = false;

        [FoldoutGroup("Custom Validation (Example)")]
        [SerializeField, Tooltip("Пример: запретить сброс предметов в этот инвентарь")]
        private bool _preventDropToInventory = false;

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Item is ItemSOAdapter adapter)
            {
                var itemSO = adapter.item;
                items.Add(itemSO);
            }
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context.Item is ItemSOAdapter adapter)
            {
                var itemSO = adapter.item;
                items.Remove(itemSO);
            }
        }

        public override void ReloadUI()
        {
            if (Application.isPlaying == false)
                return;

            if (_inventory == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Cannot sync - inventory is null");
                return;
            }

            _inventory.ClearAll();

            if (items == null || items.Count == 0)
                return;

            foreach (var itemSO in items)
            {
                // Пропускаем null элементы
                if (itemSO == null)
                    continue;

                // Автоматический выбор правильного адаптера
                IInventoryItem itemAdapter = new ItemSOAdapter(itemSO);

                AddToUIQuiet(itemAdapter, 1);
            }
        }

        #region Custom Validation Examples

        /// <summary>
        /// ПРИМЕР: Переопределение проверки начала перетаскивания
        /// Здесь можно добавить кастомную логику, например:
        /// - Запретить перетаскивание определенных предметов
        /// - Проверить условия игры (заблокирован ли инвентарь, достаточно ли прав у игрока и т.д.)
        /// </summary>
        protected override RuleResult CanStartDragInternal(DragContext context, DragEntry entry)
        {
            // Пример: запрещаем перетаскивание из этого инвентаря
            if (_preventDragFromInventory)
            {
                return RuleResult.Failure("Перетаскивание из этого инвентаря запрещено (пример)");
            }

            // Вызываем базовую реализацию (по умолчанию разрешает)
            return base.CanStartDragInternal(context, entry);
        }

        /// <summary>
        /// ПРИМЕР: Переопределение проверки сброса предмета
        /// Здесь можно добавить кастомную логику, например:
        /// - Проверить максимальный вес инвентаря
        /// - Проверить уровень игрока
        /// - Запретить сброс предметов определенного типа
        /// </summary>
        protected override RuleResult CanDropInternal(DragContext context, DragEntry entry)
        {
            // Пример: запрещаем сброс предметов в этот инвентарь
            if (_preventDropToInventory)
            {
                return RuleResult.Failure("Сброс предметов в этот инвентарь запрещен (пример)");
            }

            // Вызываем базовую реализацию (по умолчанию разрешает)
            return base.CanDropInternal(context, entry);
        }

        #endregion
    }
}
