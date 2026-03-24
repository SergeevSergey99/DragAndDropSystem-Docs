using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// Binding для слота результата крафта (1 слот).
    /// Блокирует входящие дропы. Когда игрок забирает результат — вызывает OnResultTaken,
    /// чтобы CraftingManager мог списать ингредиенты.
    /// </summary>
    public class CraftingOutputBinding : InventoryDataBindingBase
    {
        /// <summary>
        /// Вызывается когда игрок забрал предмет из слота результата.
        /// </summary>
        public event Action OnResultTaken;

        private IInventoryItem _currentResult;
        private int _currentResultCount;

        /// <summary>
        /// Показать результат рецепта в слоте. Вызывается CraftingManager'ом.
        /// </summary>
        public void SetResult(IInventoryItem item, int count)
        {
            _currentResult = item;
            _currentResultCount = count;
            ReloadUI();
        }

        /// <summary>
        /// Очистить слот результата.
        /// </summary>
        public void ClearResult()
        {
            _currentResult = null;
            _currentResultCount = 0;
            ReloadUI();
        }

        protected override void OnReloadUI()
        {
            if (_currentResult != null)
                AddToUIQuiet(_currentResult, _currentResultCount, 0);
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            // Предметы добавляются только программно через SetResult
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            // Игрок забрал результат — уведомляем CraftingManager
            _currentResult = null;
            _currentResultCount = 0;
            OnResultTaken?.Invoke();
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            return RuleResult.Failure("Cannot drop items into crafting output");
        }
    }
}
