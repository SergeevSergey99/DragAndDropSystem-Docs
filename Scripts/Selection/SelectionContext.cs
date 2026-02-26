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
        private readonly HashSet<ISlot> _selectedSet;

        /// <summary>
        /// Выделенные слоты, сгруппированные по инвентарям.
        /// Позволяет применять разную логику к слотам из разных инвентарей (например, разные наценки у торговцев).
        /// </summary>
        public IReadOnlyDictionary<IInventory, IReadOnlyList<ISlot>> ByInventory { get; }

        /// <summary>
        /// Все выделенные слоты — плоский список для простых действий, которым не важен инвентарь.
        /// </summary>
        public IReadOnlyList<ISlot> AllSlots { get; }

        public bool HasSelection   => AllSlots.Count > 0;
        public int TotalSlotsCount => AllSlots.Count;
        public int InventoryCount  => ByInventory.Count;

        /// <summary>
        /// O(1) проверка — выделен ли данный слот. Используется в SlotSelectionView.
        /// </summary>
        public bool Contains(ISlot slot) => slot != null && _selectedSet.Contains(slot);

        /// <summary>
        /// Пустой контекст — нет выделения. Используется как начальное состояние.
        /// </summary>
        public static readonly SelectionContext Empty = new SelectionContext(
            new Dictionary<IInventory, List<ISlot>>(),
            new List<ISlot>(),
            new HashSet<ISlot>()
        );

        /// <summary>
        /// Конструктор internal — только SelectionManager может создавать контекст.
        /// </summary>
        internal SelectionContext(
            Dictionary<IInventory, List<ISlot>> byInventory,
            List<ISlot> allSlots,
            HashSet<ISlot> selectedSet)
        {
            _selectedSet = new HashSet<ISlot>(selectedSet);
            AllSlots     = allSlots.AsReadOnly();

            var snapshot = new Dictionary<IInventory, IReadOnlyList<ISlot>>(byInventory.Count);
            foreach (var kvp in byInventory)
                snapshot[kvp.Key] = kvp.Value.AsReadOnly();
            ByInventory = snapshot;
        }
    }
}
