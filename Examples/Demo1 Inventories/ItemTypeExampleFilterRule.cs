using System.Linq;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Rules;
using UnityEngine;

namespace UniversalDragAndDrop.Examples.General
{
    public class ItemTypeExampleFilterRule : DragRuleBase, IInventoryRule, ISlotRule
    {
        [SerializeField]
        [Tooltip("IDs of allowed/disallowed items")]
        private string[] _allowedTypes = new string[0];

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack == null || entry.Stack.PrimaryAdapter == null)
                return RuleResult.Failure("Invalid itemAdapter");

            if (entry.Stack.PrimaryAdapter is ItemAdapterSoAdapter adapter)
            {
                if (_allowedTypes.Contains(adapter.item.itemType))
                    return RuleResult.Success();
                return RuleResult.Failure($"PrimaryAdapter {entry.Stack.DisplayName} has wrong type");
            }
            return RuleResult.Failure($"PrimaryAdapter {entry.Stack.DisplayName} has wrong adapter");
        }
    }
}