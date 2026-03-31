using System.Linq;
using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.Rules
{
    public class RuleNameFilter : DragRuleBase, IInventoryRule, ISlotRule
    {
        [SerializeField]
        [Tooltip("ID разрешенных/запрещенных имен")]
        private string[] _names = new string[0];

        [SerializeField]
        [Tooltip("Тип фильтрации по имени")]
        private NameFilterType _filterType = NameFilterType.Whitelist;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack == null || entry.Stack.ItemAdapter == null)
                return RuleResult.Failure("Invalid itemAdapter");

            if (_filterType == NameFilterType.Whitelist)
            {
                if (_names.Contains(entry.Stack.DisplayName))
                    return RuleResult.Success();
                return RuleResult.Failure($"ItemAdapter {entry.Stack.DisplayName} is not in whitelist");
            }
            else // Blacklist
            {
                if (_names.Contains(entry.Stack.DisplayName))
                    return RuleResult.Failure($"ItemAdapter {entry.Stack.DisplayName} is in blacklist");
                return RuleResult.Success();
            }
        }
    }

    public enum NameFilterType
    {
        Whitelist,
        Blacklist
    }
}