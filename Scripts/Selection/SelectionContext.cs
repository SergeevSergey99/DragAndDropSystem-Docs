using System.Collections.Generic;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Неизменяемый снимок текущего состояния выделения.
    /// Создаётся SelectionManager при каждом изменении выделения.
    /// Публичные свойства доступны только для чтения — изменить состояние можно только через SelectionManager.
    /// </summary>
    public sealed class SelectionContext
    {
        // Приватный HashSet для O(1) поиска — не доступен снаружи
        private readonly HashSet<BaseSlot> _selectedSet;

        /// <summary>
        /// Выделенные слоты, сгруппированные по инвентарям.
        /// Позволяет применять разную логику к слотам из разных инвентарей (например, разные наценки у торговцев).
        /// </summary>
        public IReadOnlyDictionary<IInventory, IReadOnlyList<BaseSlot>> ByInventory { get; }

        /// <summary>
        /// Все выделенные слоты — плоский список для простых действий, которым не важен инвентарь.
        /// </summary>
        public IReadOnlyList<BaseSlot> AllSlots { get; }

        public bool HasSelection   => AllSlots.Count > 0;
        public int TotalSlotsCount => AllSlots.Count;
        public int InventoryCount  => ByInventory.Count;

        /// <summary>
        /// O(1) проверка — выделен ли данный слот. Используется в SlotSelectionView.
        /// </summary>
        public bool Contains(BaseSlot baseSlot) => baseSlot != null && _selectedSet.Contains(baseSlot);

        /// <summary>
        /// Пустой контекст — нет выделения. Используется как начальное состояние.
        /// </summary>
        public static readonly SelectionContext Empty = new SelectionContext(
            new Dictionary<IInventory, List<BaseSlot>>(),
            new List<BaseSlot>(),
            new HashSet<BaseSlot>()
        );

        /// <summary>
        /// Конструктор internal — только SelectionManager может создавать контекст.
        /// </summary>
        internal SelectionContext(
            Dictionary<IInventory, List<BaseSlot>> byInventory,
            List<BaseSlot> allSlots,
            HashSet<BaseSlot> selectedSet)
        {
            _selectedSet = new HashSet<BaseSlot>(selectedSet);
            AllSlots     = allSlots.AsReadOnly();

            var snapshot = new Dictionary<IInventory, IReadOnlyList<BaseSlot>>(byInventory.Count);
            foreach (var kvp in byInventory)
                snapshot[kvp.Key] = new List<BaseSlot>(kvp.Value).AsReadOnly();
            ByInventory = snapshot;
        }
    }
}
