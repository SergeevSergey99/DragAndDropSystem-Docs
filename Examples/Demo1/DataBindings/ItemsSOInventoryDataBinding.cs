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
    public class ItemsSOInventoryDataBinding : ListInventoryDataBinding<ItemExampleSO, ItemSOAdapter>
    {
        [FoldoutGroup("Data")]
        [SerializeField]
        [OnValueChanged(nameof(ReloadUI))]
        private List<ItemExampleSO> items;

        [FoldoutGroup("Custom Validation (Example)")]
        [SerializeField, Tooltip("Пример: запретить перетаскивание из этого инвентаря")]
        private bool _preventDragFromInventory = false;

        [FoldoutGroup("Custom Validation (Example)")]
        [SerializeField, Tooltip("Пример: запретить сброс предметов в этот инвентарь")]
        private bool _preventDropToInventory = false;

        protected override IReadOnlyList<ItemExampleSO> GetItems() => items;
        protected override ItemSOAdapter CreateAdapter(ItemExampleSO item) => new(item);
        protected override void AddToData(ItemSOAdapter adapter) => items.Add(adapter.item);
        protected override void RemoveFromData(ItemSOAdapter adapter) => items.Remove(adapter.item);

        /// <summary>
        /// ПРИМЕР: Переопределение проверки начала перетаскивания
        /// Здесь можно добавить кастомную логику, например:
        /// - Запретить перетаскивание определенных предметов
        /// - Проверить условия игры (заблокирован ли инвентарь, достаточно ли прав у игрока и т.д.)
        /// </summary>
        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            // Пример: запрещаем перетаскивание из этого инвентаря
            if (_preventDragFromInventory)
            {
                return RuleResult.Failure("Перетаскивание из этого инвентаря запрещено (пример)");
            }

            // Вызываем базовую реализацию (по умолчанию разрешает)
            return base.CanStartDrag(context, entry);
        }

        /// <summary>
        /// ПРИМЕР: Переопределение проверки сброса предмета
        /// Здесь можно добавить кастомную логику, например:
        /// - Проверить максимальный вес инвентаря
        /// - Проверить уровень игрока
        /// - Запретить сброс предметов определенного типа
        /// </summary>
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Пример: запрещаем сброс предметов в этот инвентарь
            if (_preventDropToInventory)
            {
                return RuleResult.Failure("Сброс предметов в этот инвентарь запрещен (пример)");
            }

            // Вызываем базовую реализацию (по умолчанию разрешает)
            return base.CanDrop(context, entry);
        }
    }
}
