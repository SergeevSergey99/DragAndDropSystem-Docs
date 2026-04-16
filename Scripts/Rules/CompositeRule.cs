using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Rules
{
    public class CompositeRule<TRule> : DragRuleBase where TRule : IDragRule
    {
        [SerializeField]
        private CompositeRuleMode _mode = CompositeRuleMode.AND;

        [SerializeField, HideLabel]
        private RuleValidator<TRule> _validator = new RuleValidator<TRule>();

        private RuleResult EvaluateRules(DragContext context, DragEntry entry, System.Func<TRule, RuleResult> evaluator)
        {
            var rules = _validator.GetRules();

            foreach (var rule in rules)
            {
                var result = evaluator(rule);

                if (_mode == CompositeRuleMode.AND && !result.IsValid)
                    return result;

                if (_mode == CompositeRuleMode.OR && result.IsValid)
                    return RuleResult.Success();
            }

            return _mode == CompositeRuleMode.AND
                ? RuleResult.Success()
                : RuleResult.Failure("All rules failed in OR composite rule.");
        }

        public override RuleResult CanStartDrag(DragContext context, DragEntry entry) =>
            EvaluateRules(context, entry, r => r.CanStartDrag(context, entry));

        public override RuleResult CanDrop(DragContext context, DragEntry entry) =>
            EvaluateRules(context, entry, r => r.CanDrop(context, entry));
    }

    public class CompositeGlobalRule : CompositeRule<IGlobalRule>, IGlobalRule { }

    public class CompositeInventoryRule : CompositeRule<IInventoryRule>, IInventoryRule { }

    public class CompositeSlotRule : CompositeRule<ISlotRule>, ISlotRule { }

    public enum CompositeRuleMode
    {
        AND,
        OR
    }
}
