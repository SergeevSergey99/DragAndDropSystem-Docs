using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Базовый класс для выделения слотов по условию.
    /// Наследуйтесь и переопределите Matches() для любой логики фильтрации:
    ///
    ///   [Serializable]
    ///   public class SelectByRarityOperation : SelectByConditionOperation
    ///   {
    ///       [SerializeField] private int _minRarity;
    ///
    ///       protected override bool Matches(ISlot slot) =>
    ///           slot.Stack._PrimaryAdapter is IFilterable f и f.Rarity >= _minRarity;
    ///   }
    /// </summary>
    [System.Serializable]
    public abstract class SelectByConditionOperation : SelectionOperationBase
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField, Tooltip("Clear the existing selection first")]
        private bool _clearFirst = true;

        /// <summary>
        /// Возвращает true если слот должен быть выделен
        /// </summary>
        protected abstract bool Matches(BaseSlot baseSlot);

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            IInventory target = _inventory != null ? _inventory : contextBaseSlot?.Inventory;
            if (target == null) return;

            if (_clearFirst)
                manager.Clear();

            foreach (var slot in target.Slots)
            {
                if (slot != null && !slot.IsEmpty && Matches(slot))
                    manager.Select(slot);
            }
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot)
               && (_inventory != null || contextBaseSlot?.Inventory != null);
    }
}
