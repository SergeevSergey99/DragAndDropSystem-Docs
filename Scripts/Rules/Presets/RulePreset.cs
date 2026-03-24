using System.Collections.Generic;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Rules
{
    /// <summary>
    /// Базовый контейнер правил, который можно переиспользовать как ScriptableObject пресет
    /// </summary>
    public abstract class RulePreset<TRule> : ScriptableObject where TRule : IDragRule
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        [Title("Inline Rules", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Правила, сохраненные внутри этого пресета")]
        private List<TRule> _inlineRules = new();

        /// <summary>
        /// Список правил, сконфигурированных внутри пресета
        /// </summary>
        public IReadOnlyList<TRule> GetRules() => _inlineRules;
    }
}
