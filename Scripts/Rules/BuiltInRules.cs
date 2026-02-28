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
            if (entry.SourceSlot == context.TargetSlot)
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
            if (entry.Stack == null || entry.Stack.Item == null)
                return RuleResult.Failure("Invalid item");

            bool contains = _allowedItemIds.Contains(entry.Stack.Item.ItemId);

            if (_whitelist && !contains)
                return RuleResult.Failure($"Item {entry.Stack.Item.DisplayName} is not allowed in this slot");

            if (!_whitelist && contains)
                return RuleResult.Failure($"Item {entry.Stack.Item.DisplayName} is not allowed in this slot");

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
            if (context.TargetInventory.Contains(entry.Stack.Item))
                return RuleResult.Success();

            // Подсчитываем уникальные предметы
            var uniqueItems = context.TargetInventory.Slots
                .Where(s => !s.IsEmpty)
                .Select(s => s.Stack.Item.ItemId)
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

    /// <summary>
    /// Опциональное правило для ограничения размера стака в инвентаре
    /// Применяется к инвентарю для ограничения количества предметов в одном слоте
    /// По умолчанию система не ограничивает размер стака - добавьте это правило если нужны лимиты
    /// </summary>
    [Serializable]
    public class MaxStackSizeRule : DragRuleBase, IInventoryRule
    {
        [SerializeField, Range(1, 9999)]
        [Tooltip("Максимальное количество предметов в одном слоте")]
        private int _maxStackSize = 999;

        public MaxStackSizeRule() { }

        public MaxStackSizeRule(int maxStackSize)
        {
            _maxStackSize = maxStackSize;
        }

        public override int Priority => 60;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (context.TargetSlot == null)
            {
                // Дроп в инвентарь без конкретного слота - проверяем стак целиком
                if (entry.Stack.Count > _maxStackSize)
                {
                    return RuleResult.Failure($"Stack size limit is {_maxStackSize}");
                }
            }
            else if (context.TargetSlot.IsEmpty)
            {
                // Дроп в пустой слот - проверяем размер дропаемого стака
                if (entry.Stack.Count > _maxStackSize)
                {
                    return RuleResult.Failure($"Stack size limit is {_maxStackSize}");
                }
            }
            else if (context.TargetSlot.Stack.CanStack(entry.Stack.Item))
            {
                // Стакаем предметы - проверяем результирующее количество
                int resultCount = context.TargetSlot.Stack.Count + entry.Stack.Count;
                if (resultCount > _maxStackSize)
                {
                    return RuleResult.Failure($"Stack size limit is {_maxStackSize} (would result in {resultCount})");
                }
            }

            return RuleResult.Success();
        }
    }
}
