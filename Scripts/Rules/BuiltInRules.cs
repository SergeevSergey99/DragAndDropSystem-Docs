using System;
using System.Linq;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Rules
{
    /// <summary>
    /// Prevents dropping an item into the same slot it was taken from
    /// Applied globally
    /// </summary>
    [Serializable]
    public class SameSlotRule : DragRuleBase, IGlobalRule
    {
        public override int Priority => 10;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // For batch operations TargetSlot is a UI hint, not the actual entry target; slot validation is handled by the execution pipeline
            if (!context.IsBatchDrag &&
                entry.SourceBaseSlot == context.TargetBaseSlot &&
                !IsChangedShapedPlacement(context, entry))
                return RuleResult.Failure("Cannot drop to the same slot");

            return RuleResult.Success();
        }

        private static bool IsChangedShapedPlacement(DragContext context, DragEntry entry)
        {
            if (!entry.IsShaped ||
                entry.SourcePlacement == null ||
                context.TargetBaseSlot == null ||
                entry.SourceInventory is not UniversalInventory inventory ||
                !ReferenceEquals(context.TargetInventory, inventory) ||
                !ReferenceEquals(context.TargetBaseSlot.Inventory, inventory))
                return false;

            if (!inventory.TryResolveShapedPlacementAnchor(
                    context.TargetBaseSlot,
                    context,
                    entry,
                    entry.Footprint,
                    entry.Stack?.PrimaryAdapter,
                    out _,
                    out int anchorIndex))
                return false;

            return anchorIndex != entry.SourcePlacement.AnchorIndex ||
                   entry.Orientation != entry.SourcePlacement.Orientation;
        }
    }

    /// <summary>
    /// Rule for moving items within the same inventory
    /// Applied to inventories
    /// </summary>
    [Serializable]
    public class SameInventoryRule : DragRuleBase, IInventoryRule
    {
        [SerializeField]
        [Tooltip("Allow moving items within the same inventory")]
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
    /// Rule for filtering items by type
    /// Universal rule that can be applied to inventories and slots
    /// </summary>
    [Serializable]
    public class ItemIdFilterRule : DragRuleBase, IInventoryRule, ISlotRule
    {
        [SerializeField]
        [Tooltip("IDs of allowed/disallowed items")]
        private string[] _allowedItemIds = new string[0];

        [SerializeField]
        [Tooltip("true = allow only these items (whitelist), false = disallow these items (blacklist)")]
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
    /// Rule that limits the maximum number of unique items in an inventory
    /// Applied to inventories
    /// </summary>
    [Serializable]
    public class UniqueItemLimitRule : DragRuleBase, IInventoryRule
    {
        [SerializeField]
        [Tooltip("Maximum number of unique items in the inventory")]
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

            // If the item is already present in the inventory, allow it
            if (context.TargetInventory.Contains(entry.Stack.PrimaryAdapter))
                return RuleResult.Success();

            // Count unique items
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
    /// Rule for locking specific slots
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
    /// Custom rule based on lambdas
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
