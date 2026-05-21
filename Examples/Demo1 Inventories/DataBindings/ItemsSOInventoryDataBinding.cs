using System.Collections.Generic;
using UDND.Core;
using UDND.DataBinding;
using UDND.Examples.General;
using UDND.Tools.Inspector;
using UDND.Rules;
using UnityEngine;

namespace UDND.Examples.General
{
    /// <summary>
    /// Universal DataBinding for ItemExampleSO and ItemExampleWith3DSO
    /// Automatically determines the correct adapter
    ///
    /// EXAMPLE: Demonstrates inventory rule usage and overriding transfer validation methods
    /// </summary>
    public class ItemsSOInventoryDataBinding : ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>
    {
        [FoldoutGroup("Data")]
        [SerializeField]
        [OnValueChanged(nameof(ReloadUI))]
        private List<ItemExampleSO> items;

        [FoldoutGroup("Custom Validation (Example)")]
        [SerializeField, Tooltip("Example: prevent dragging from this inventory")]
        private bool _preventDragFromInventory = false;

        [FoldoutGroup("Custom Validation (Example)")]
        [SerializeField, Tooltip("Example: prevent dropping items into this inventory")]
        private bool _preventDropToInventory = false;

        protected override IReadOnlyList<ItemExampleSO> GetItems() => items;
        protected override ItemAdapterSoAdapter CreateAdapter(ItemExampleSO item) => new(item);
        protected override void AddToData(ItemAdapterSoAdapter adapter) => items.Add(adapter.item);
        protected override void RemoveFromData(ItemAdapterSoAdapter adapter) => items.Remove(adapter.item);

        /// <summary>
        /// EXAMPLE: Override of drag-start validation
        /// Custom logic can be added here, for example:
        /// - Forbid dragging specific items
        /// - Check game conditions (whether the inventory is locked, whether the player has permission, etc.)
        /// </summary>
        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            // Example: forbid dragging from this inventory
            if (_preventDragFromInventory)
            {
                return RuleResult.Failure("Dragging from this inventory is disabled (example)");
            }

            // Call the base implementation (allowed by default)
            return base.CanStartDrag(context, entry);
        }

        /// <summary>
        /// EXAMPLE: Override of drop validation
        /// Custom logic can be added here, for example:
        /// - Check the maximum inventory weight
        /// - Check player level
        /// - Forbid dropping certain item types
        /// </summary>
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Example: forbid dropping items into this inventory
            if (_preventDropToInventory)
            {
                return RuleResult.Failure("Dropping items into this inventory is disabled (example)");
            }

            // Call the base implementation (allowed by default)
            return base.CanDrop(context, entry);
        }
    }
}