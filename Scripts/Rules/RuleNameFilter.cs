using System.Linq;
using UnityEngine;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Rules
{
    public class RuleNameFilter : DragRuleBase, IInventoryRule, ISlotRule
    {
        [SerializeField]
        [Tooltip("IDs of allowed/disallowed names")]
        private string[] _names = new string[0];

        [SerializeField]
        [Tooltip("Name filtering type")]
        private NameFilterType _filterType = NameFilterType.Whitelist;

        public override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack == null || entry.Stack.PrimaryAdapter == null)
                return RuleResult.Failure("Invalid itemAdapter");

            if (_filterType == NameFilterType.Whitelist)
            {
                if (_names.Contains(entry.Stack.DisplayName))
                    return RuleResult.Success();
                return RuleResult.Failure($"_PrimaryAdapter {entry.Stack.DisplayName} is not in whitelist");
            }
            else // Blacklist
            {
                if (_names.Contains(entry.Stack.DisplayName))
                    return RuleResult.Failure($"_PrimaryAdapter {entry.Stack.DisplayName} is in blacklist");
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