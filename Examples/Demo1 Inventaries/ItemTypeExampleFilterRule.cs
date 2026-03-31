using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.Examples;
using DragAndDropSystem.Rules;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples
{
    public class ItemTypeExampleFilterRule : DragRuleBase, IInventoryRule, ISlotRule
    {
        [SerializeField]
        [Tooltip("ID разрешенных/запрещенных предметов")]
        private string[] _allowedTypes = new string[0];

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack == null || entry.Stack.ItemAdapter == null)
                return RuleResult.Failure("Invalid itemAdapter");

            if (entry.Stack.ItemAdapter is ItemAdapterSoAdapter adapter)
            {
                if (_allowedTypes.Contains(adapter.item.itemType))
                    return RuleResult.Success();
                return RuleResult.Failure($"ItemAdapter {entry.Stack.DisplayName} has wrong type");
            }
            return RuleResult.Failure($"ItemAdapter {entry.Stack.DisplayName} has wrong adapter");
        }
    }
}