using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Rules
{
    /// <summary>
    /// Generic валидатор, проверяющий все правила определенного типа
    /// Поддерживает настройку через Inspector
    /// Обеспечивает типобезопасность через generic ограничение
    /// </summary>
    [Serializable]
    public class RuleValidator<TRule> where TRule : IDragRule
    {
        [SerializeField, Title("Preset Lists", TitleAlignment = TitleAlignments.Centered)]
        [ListDrawerSettings(
            DraggableItems = true,
            ShowPaging = false,
            ShowFoldout = true,
            ShowIndexLabels = true
            //, ListElementLabelName = "@$property.ValueEntry.WeakSmartValue is UnityEngine.Object obj ? obj.name : \"Null\""
        )]
        [Tooltip("Наборы правил, задаваемые через ScriptableObject пресеты")]
        private List<RulePreset<TRule>> _presets = new List<RulePreset<TRule>>();

        [SerializeReference, Title("Inline Rules", TitleAlignment = TitleAlignments.Centered)]
        [ListDrawerSettings(
            DraggableItems = true,
            ShowPaging = false,
            ShowFoldout = true,
            ShowIndexLabels = true
            //, ListElementLabelName = "@$property.ValueEntry.WeakSmartValue is DragAndDropSystem.Rules.IDragRule rule && !string.IsNullOrEmpty(rule.RuleName) ? rule.RuleName : \"Null Rule\""
        )]
        [Tooltip("Локальные inline правила, специфичные для текущего владельца")]
        private List<TRule> _inlineRules = new List<TRule>();

        [NonSerialized]
        private readonly List<TRule> _combinedRules = new List<TRule>();

        public RuleValidator()
        {
        }

        /// <summary>
        /// Добавить правило программно
        /// </summary>
        public void AddRule(TRule rule)
        {
            if (rule == null) return;

            _inlineRules.Add(rule);
        }

        /// <summary>
        /// Удалить inline правило
        /// </summary>
        public void RemoveRule(TRule rule)
        {
            _inlineRules.Remove(rule);
        }

        /// <summary>
        /// Очистить все inline правила
        /// </summary>
        public void ClearRules()
        {
            _inlineRules.Clear();
        }

        /// <summary>
        /// Добавить пресет правил
        /// </summary>
        public void AddPreset(RulePreset<TRule> preset)
        {
            if (preset == null || _presets.Contains(preset)) return;
            _presets.Add(preset);
        }

        /// <summary>
        /// Удалить пресет правил
        /// </summary>
        public void RemovePreset(RulePreset<TRule> preset)
        {
            if (preset == null) return;
            _presets.Remove(preset);
        }

        /// <summary>
        /// Очистить список пресетов
        /// </summary>
        public void ClearPresets()
        {
            _presets.Clear();
        }

        /// <summary>
        /// Получить все правила (для отладки)
        /// </summary>
        public IReadOnlyList<TRule> GetRules() => BuildCombinedRules(null);

        internal IReadOnlyList<TRule> GetRules(HashSet<RulePreset<TRule>> visitedPresets) =>
            BuildCombinedRules(visitedPresets);

        /// <summary>
        /// Валидация начала перетаскивания для конкретного entry
        /// </summary>
        public RuleResult ValidateStartDrag(DragContext context, DragEntry entry)
        {
            foreach (var rule in BuildCombinedRules(null))
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
        /// Валидация сброса предмета для конкретного entry
        /// </summary>
        public RuleResult ValidateDrop(DragContext context, DragEntry entry)
        {
            foreach (var rule in BuildCombinedRules(null))
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

        private IReadOnlyList<TRule> BuildCombinedRules(HashSet<RulePreset<TRule>> visitedPresets)
        {
            _combinedRules.Clear();
            var visited = visitedPresets ?? new HashSet<RulePreset<TRule>>();

            // Добавляем правила из пресетов
            foreach (var preset in _presets)
            {
                if (preset == null) continue;

                if (!visited.Add(preset))
                {
                    Debug.LogWarning($"Circular rule preset reference detected: {preset.name}", preset);
                    continue;
                }

                var presetRules = preset.InternalGetRules(visited);
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

                visited.Remove(preset);
            }

            // Добавляем локальные inline правила
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
        /// Метод для валидации в Editor (вызывается при изменениях в Inspector)
        /// </summary>
        public void OnValidate()
        {
            BuildCombinedRules(null);
        }
    }

    /// <summary>
    /// Валидатор для глобальных правил (применяется в DragAndDropManager)
    /// </summary>
    [Serializable]
    public class GlobalRuleValidator : RuleValidator<IGlobalRule>
    {
    }

    /// <summary>
    /// Валидатор для правил инвентаря (применяется в UniversalInventory)
    /// </summary>
    [Serializable]
    public class InventoryRuleValidator : RuleValidator<IInventoryRule>
    {
    }

    /// <summary>
    /// Валидатор для правил слота (применяется в UniversalSlot)
    /// </summary>
    [Serializable]
    public class SlotRuleValidator : RuleValidator<ISlotRule>
    {
    }
}
