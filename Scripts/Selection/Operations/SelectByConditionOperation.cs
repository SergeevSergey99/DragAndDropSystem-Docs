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
    ///           slot.Stack.Item is IFilterable f и f.Rarity >= _minRarity;
    ///   }
    /// </summary>
    [System.Serializable]
    public abstract class SelectByConditionOperation : SelectionOperationBase
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField, Tooltip("Сначала снять существующее выделение")]
        private bool _clearFirst = true;

        /// <summary>
        /// Возвращает true если слот должен быть выделен
        /// </summary>
        protected abstract bool Matches(ISlot slot);

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
        {
            IInventory target = _inventory != null ? _inventory : contextSlot?.Inventory;
            if (target == null) return;

            if (_clearFirst)
                manager.Clear();

            foreach (var slot in target.Slots)
            {
                if (slot != null && !slot.IsEmpty && Matches(slot))
                    manager.Select(slot);
            }
        }

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot)
               && (_inventory != null || contextSlot?.Inventory != null);
    }
}
