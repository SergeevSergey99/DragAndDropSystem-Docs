using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Rules
{
    /// <summary>
    /// Base rule container that can be reused as a ScriptableObject preset
    /// </summary>
    public abstract class RulePreset<TRule> : ScriptableObject where TRule : IDragRule
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        [Title("Inline Rules", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Rules stored inside this preset")]
        private List<TRule> _inlineRules = new();

        /// <summary>
        /// List of rules configured inside the preset
        /// </summary>
        public IReadOnlyList<TRule> GetRules() => _inlineRules;
    }
}
