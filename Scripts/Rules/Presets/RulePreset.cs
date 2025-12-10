using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Rules
{
    /// <summary>
    /// Базовый контейнер правил, который можно переиспользовать как ScriptableObject пресет
    /// </summary>
    public abstract class RulePreset<TRule> : ScriptableObject where TRule : IDragRule
    {
        [SerializeField, InlineProperty, HideLabel]
        [Title("Inline Rules", TitleAlignment = TitleAlignments.Centered)]
        private RuleValidator<TRule> _ruleValidator = new RuleValidator<TRule>();

        /// <summary>
        /// Список правил, сконфигурированных внутри пресета
        /// </summary>
        public IReadOnlyList<TRule> GetRules() => _ruleValidator.GetRules();

        internal IReadOnlyList<TRule> InternalGetRules(HashSet<RulePreset<TRule>> visited)
        {
            return _ruleValidator.GetRules(visited);
        }

        /// <summary>
        /// Доступ к валидатору для настройки через инспектор или код
        /// </summary>
        public RuleValidator<TRule> Validator => _ruleValidator;

        protected virtual void OnValidate()
        {
            _ruleValidator?.OnValidate();
        }
    }
}
