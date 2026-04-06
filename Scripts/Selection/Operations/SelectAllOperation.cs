using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Выделяет все слоты в инвентаре (Ctrl+A).
    /// Если _inventory не задан — использует инвентарь contextSlot.
    /// </summary>
    [System.Serializable]
    public class SelectAllOperation : SelectionOperationBase
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField, Tooltip("Clear the existing selection first")]
        private bool _clearFirst = true;

        public override string DisplayName => "Select All";

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
        {
            IInventory target = _inventory != null ? _inventory : contextSlot?.Inventory;
            if (target == null) return;

            if (_clearFirst)
                manager.Clear();

            manager.SelectAll(target);
        }

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot)
               && (_inventory != null || contextSlot?.Inventory != null);
    }
}
