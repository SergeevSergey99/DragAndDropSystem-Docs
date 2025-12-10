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
        
        public override RuleResult CanDrop(DragContext context)
        {
            if (context.DraggedStack == null || context.DraggedStack.Item == null)
                return RuleResult.Failure("Invalid item");

            if (_filterType == NameFilterType.Whitelist)
            {
                if (_names.Contains(context.DraggedStack.Item.DisplayName))
                    return RuleResult.Success();
                return RuleResult.Failure($"Item {context.DraggedStack.Item.DisplayName} is not in whitelist");
            }
            else // Blacklist
            {
                if (_names.Contains(context.DraggedStack.Item.DisplayName))
                    return RuleResult.Failure($"Item {context.DraggedStack.Item.DisplayName} is in blacklist");
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