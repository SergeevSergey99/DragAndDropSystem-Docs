using DragAndDropSystem.Core;
using System;
using System.Linq;
using UnityEngine;

namespace DragAndDropSystem.Rules
{
    /// <summary>
    /// Запрещает бросать предмет в тот же слот, откуда взяли
    /// Применяется глобально
    /// </summary>
    [Serializable]
    public class SameSlotRule : DragRuleBase, IGlobalRule
    {
        public override int Priority => 10;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Для batch TargetSlot — UI-хинт, не реальная цель entry; slot-валидация выполняется execution pipeline
            if (!context.IsBatchDrag && entry.SourceSlot == context.TargetSlot)
                return RuleResult.Failure("Cannot drop to the same slot");

            return RuleResult.Success();
        }
    }

    /// <summary>
    /// Правило для перемещения внутри одного инвентаря
    /// Применяется к инвентарям
    /// </summary>
    [Serializable]
    public class SameInventoryRule : DragRuleBase, IInventoryRule
    {
        [SerializeField]
        [Tooltip("Разрешить перемещение предметов внутри одного инвентаря")]
        private bool _allowMoveWithinInventory = true;

        public SameInventoryRule() { }

        public SameInventoryRule(bool allowMoveWithinInventory = true)
        {
            _allowMoveWithinInventory = allowMoveWithinInventory;
        }

        public override int Priority => 20;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.SourceInventory == context.TargetInventory && !_allowMoveWithinInventory)
                return RuleResult.Failure("Moving within same inventory is not allowed");

            return RuleResult.Success();
        }
    }

    /// <summary>
    /// Правило для фильтрации предметов по типу
    /// Универсальное правило - применимо к инвентарям и слотам
    /// </summary>
    [Serializable]
    public class ItemIdFilterRule : DragRuleBase, IInventoryRule, ISlotRule
    {
        [SerializeField]
        [Tooltip("ID разрешенных/запрещенных предметов")]
        private string[] _allowedItemIds = new string[0];

        [SerializeField]
        [Tooltip("true = разрешить только эти предметы (whitelist), false = запретить эти предметы (blacklist)")]
        private bool _whitelist = true;

        public ItemIdFilterRule() { }

        public ItemIdFilterRule(string[] allowedItemIds, bool whitelist = true)
        {
            _allowedItemIds = allowedItemIds;
            _whitelist = whitelist;
        }

        public override int Priority => 50;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack == null || entry.Stack.PrimaryAdapter == null)
                return RuleResult.Failure("Invalid itemAdapter");

            bool contains = _allowedItemIds.Contains(entry.Stack.ID);

            if (_whitelist && !contains)
                return RuleResult.Failure($"PrimaryAdapter {entry.Stack.DisplayName} is not allowed in this slot");

            if (!_whitelist && contains)
                return RuleResult.Failure($"PrimaryAdapter {entry.Stack.DisplayName} is not allowed in this slot");

            return RuleResult.Success();
        }
    }

    /// <summary>
    /// Правило максимального количества уникальных предметов в инвентаре
    /// Применяется к инвентарям
    /// </summary>
    [Serializable]
    public class UniqueItemLimitRule : DragRuleBase, IInventoryRule
    {
        [SerializeField]
        [Tooltip("Максимальное количество уникальных предметов в инвентаре")]
        [Range(1, 100)]
        private int _maxUniqueItems = 50;

        public UniqueItemLimitRule() { }

        public UniqueItemLimitRule(int maxUniqueItems)
        {
            _maxUniqueItems = maxUniqueItems;
        }

        public override int Priority => 60;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (context.TargetInventory == null)
                return RuleResult.Failure("No target inventory");

            // Если предмет уже есть в инвентаре, разрешаем
            if (context.TargetInventory.Contains(entry.Stack.PrimaryAdapter))
                return RuleResult.Success();

            // Подсчитываем уникальные предметы
            var uniqueItems = context.TargetInventory.Slots
                .Where(s => !s.IsEmpty)
                .Select(s => s.Stack.ID)
                .Distinct()
                .Count();

            if (uniqueItems >= _maxUniqueItems)
                return RuleResult.Failure($"Inventory can only hold {_maxUniqueItems} unique items");

            return RuleResult.Success();
        }
    }

    /// <summary>
    /// Правило для блокировки определенных слотов
    /// </summary>
    public class SlotLockRule : DragRuleBase, IInventoryRule
    {
        private readonly System.Func<DragContext, DragEntry, bool> _isSlotLocked;

        public SlotLockRule(System.Func<DragContext, DragEntry, bool> isSlotLocked)
        {
            _isSlotLocked = isSlotLocked;
        }

        public override int Priority => 5;

        public override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            if (_isSlotLocked(context, entry))
                return RuleResult.Failure("Slot is locked");

            return RuleResult.Success();
        }

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (_isSlotLocked(context, entry))
                return RuleResult.Failure("Target slot is locked");

            return RuleResult.Success();
        }
    }

    /// <summary>
    /// Кастомное правило с лямбдами
    /// </summary>
    public class CustomRule : DragRuleBase, IInventoryRule
    {
        private readonly System.Func<DragContext, DragEntry, RuleResult> _canStartDragFunc;
        private readonly System.Func<DragContext, DragEntry, RuleResult> _canDropFunc;

        public CustomRule(
            System.Func<DragContext, DragEntry, RuleResult> canStartDrag = null,
            System.Func<DragContext, DragEntry, RuleResult> canDrop = null,
            int priority = 100)
        {
            _canStartDragFunc = canStartDrag;
            _canDropFunc = canDrop;
            Priority = priority;
        }

        public override int Priority { get; }

        public override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            return _canStartDragFunc?.Invoke(context, entry) ?? RuleResult.Success();
        }

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            return _canDropFunc?.Invoke(context, entry) ?? RuleResult.Success();
        }
    }

}
