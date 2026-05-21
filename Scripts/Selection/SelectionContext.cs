using System.Collections.Generic;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Selection
{
    /// <summary>
    /// Immutable snapshot of the current selection state.
    /// Created by SelectionManager every time selection changes.
    /// Public properties are read-only; state can only be changed through SelectionManager.
    /// </summary>
    public sealed class SelectionContext
    {
        // Private HashSet for O(1) lookups; not exposed externally
        private readonly HashSet<BaseSlot> _selectedSet;

        /// <summary>
        /// Selected slots grouped by inventory.
        /// Allows applying different logic to slots from different inventories (for example, different merchant markups).
        /// </summary>
        public IReadOnlyDictionary<IInventory, IReadOnlyList<BaseSlot>> ByInventory { get; }

        /// <summary>
        /// All selected slots as a flat list for simple actions where inventory grouping does not matter.
        /// </summary>
        public IReadOnlyList<BaseSlot> AllSlots { get; }

        public bool HasSelection   => AllSlots.Count > 0;
        public int TotalSlotsCount => AllSlots.Count;
        public int InventoryCount  => ByInventory.Count;

        /// <summary>
        /// O(1) check whether a given slot is selected. Used by SlotSelectionView.
        /// </summary>
        public bool Contains(BaseSlot baseSlot) => baseSlot != null && _selectedSet.Contains(baseSlot);

        /// <summary>
        /// Empty context with no selection. Used as the initial state.
        /// </summary>
        public static readonly SelectionContext Empty = new SelectionContext(
            new Dictionary<IInventory, List<BaseSlot>>(),
            new List<BaseSlot>(),
            new HashSet<BaseSlot>()
        );

        /// <summary>
        /// Internal constructor: only SelectionManager can create a context.
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