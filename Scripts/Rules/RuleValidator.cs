using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Tools.Inspector;

namespace UDND.Rules
{
    /// <summary>
    /// Generic validator that checks all rules of a given type
    /// Supports configuration through the Inspector
    /// Ensures type safety through a generic constraint
    /// </summary>
    [Serializable]
    public class RuleValidator<TRule> where TRule : IDragRule
    {
        [SerializeField, RulePresetPicker]
        [ListDrawerSettings(
            DraggableItems = true,
            ShowPaging = false,
            ShowFoldout = true,
            ShowIndexLabels = true
            //, ListElementLabelName = "@$property.ValueEntry.WeakSmartValue is UnityEngine.Object obj ? obj.name : \"Null\""
        )]
        [Tooltip("Rule sets defined via ScriptableObject presets")]
        private List<RulePreset<TRule>> _presets = new List<RulePreset<TRule>>();

        [SerializeReference, ManagedReferencePicker]
        [ListDrawerSettings(
            DraggableItems = true,
            ShowPaging = false,
            ShowFoldout = true,
            ShowIndexLabels = true
            //, ListElementLabelName = "@$property.ValueEntry.WeakSmartValue is UDND.Rules.IDragRule rule && !string.IsNullOrEmpty(rule.RuleName) ? rule.RuleName : \"Null Rule\""
        )]
        [Tooltip("Local inline rules specific to the current owner")]
        private List<TRule> _inlineRules = new List<TRule>();

        [NonSerialized]
        private readonly List<TRule> _combinedRules = new List<TRule>();

        public RuleValidator()
        {
        }

        /// <summary>
        /// Add a rule programmatically
        /// </summary>
        public void AddRule(TRule rule)
        {
            if (rule == null) return;

            _inlineRules.Add(rule);
        }

        /// <summary>
        /// Remove an inline rule
        /// </summary>
        public void RemoveRule(TRule rule)
        {
            _inlineRules.Remove(rule);
        }

        /// <summary>
        /// Clear all inline rules
        /// </summary>
        public void ClearRules()
        {
            _inlineRules.Clear();
        }

        /// <summary>
        /// Add a rule preset
        /// </summary>
        public void AddPreset(RulePreset<TRule> preset)
        {
            if (preset == null || _presets.Contains(preset)) return;
            _presets.Add(preset);
        }

        /// <summary>
        /// Remove a rule preset
        /// </summary>
        public void RemovePreset(RulePreset<TRule> preset)
        {
            if (preset == null) return;
            _presets.Remove(preset);
        }

        /// <summary>
        /// Clear the preset list
        /// </summary>
        public void ClearPresets()
        {
            _presets.Clear();
        }

        /// <summary>
        /// Get all rules (for debugging)
        /// </summary>
        public IReadOnlyList<TRule> GetRules() => BuildCombinedRules();

        /// <summary>
        /// Validate drag start for a specific entry
        /// </summary>
        public RuleResult ValidateStartDrag(DragContext context, DragEntry entry)
        {
            foreach (var rule in BuildCombinedRules())
            {
                if (rule == null) continue;

                var result = rule.CanStartDrag(context, entry);
                if (!result.IsValid)
                {
                    return result;
                }
            }
            return RuleResult.Success();
        }

        /// <summary>
        /// Validate dropping an item for a specific entry
        /// </summary>
        public RuleResult ValidateDrop(DragContext context, DragEntry entry)
        {
            foreach (var rule in BuildCombinedRules())
            {
                if (rule == null) continue;

                var result = rule.CanDrop(context, entry);
                if (!result.IsValid)
                {
                    return result;
                }
            }
            return RuleResult.Success();
        }

        private IReadOnlyList<TRule> BuildCombinedRules()
        {
            _combinedRules.Clear();

            // Add rules from presets
            foreach (var preset in _presets)
            {
                if (preset == null) continue;
                
                var presetRules = preset.GetRules();
                if (presetRules == null) continue;

                foreach (var presetRule in presetRules)
                {
                    if (presetRule == null) continue;

                    if (presetRule is TRule typedRule)
                    {
                        _combinedRules.Add(typedRule);
                    }
                    else
                    {
                        Debug.LogWarning($"Rule preset {preset.name} contains incompatible rule of type {presetRule.GetType().Name}", preset);
                    }
                }
            }

            // Add local inline rules
            foreach (var rule in _inlineRules)
            {
                if (rule == null) continue;
                _combinedRules.Add(rule);
            }

            _combinedRules.Sort((a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return a.Priority.CompareTo(b.Priority);
            });

            return _combinedRules;
        }

        /// <summary>
        /// Validation method for the Editor (called when values change in the Inspector)
        /// </summary>
        public void OnValidate()
        {
            BuildCombinedRules();
        }
    }

    /// <summary>
    /// Validator for global rules (used in DragAndDropManager)
    /// </summary>
    [Serializable]
    public class GlobalRuleValidator : RuleValidator<IGlobalRule>
    {
    }

    /// <summary>
    /// Validator for inventory rules.
    /// </summary>
    [Serializable]
    public class InventoryRuleValidator : RuleValidator<IInventoryRule>
    {
    }

    /// <summary>
    /// Validator for slot rules (used in UniversalSlot)
    /// </summary>
    [Serializable]
    public class SlotRuleValidator : RuleValidator<ISlotRule>
    {
    }
}
