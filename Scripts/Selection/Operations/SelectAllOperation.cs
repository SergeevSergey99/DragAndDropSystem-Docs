using UnityEngine;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Selection
{
    /// <summary>
    /// Selects all slots in the inventory (Ctrl+A).
    /// If _inventory is not assigned, uses the contextSlot inventory.
    /// </summary>
    [System.Serializable]
    public class SelectAllOperation : SelectionOperationBase
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField, Tooltip("Clear the existing selection first")]
        private bool _clearFirst = true;

        public override string DisplayName => "Select All";

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            IInventory target = _inventory != null ? _inventory : contextBaseSlot?.Inventory;
            if (target == null) return;

            if (_clearFirst)
                manager.Clear();

            manager.SelectAll(target);
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot)
               && (_inventory != null || contextBaseSlot?.Inventory != null);
    }
}