using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.Rules
{
    /// <summary>
    /// Результат валидации правила
    /// </summary>
    public struct RuleResult
    {
        public bool IsValid { get; }
        public string FailureReason { get; }

        public RuleResult(bool isValid, string failureReason = "")
        {
            IsValid = isValid;
            FailureReason = failureReason;
            
            if (!isValid)
            {
                Debug.LogWarning($"<color=red>[RuleResult] Validation failed: {failureReason}</color>");
            }
        }

        public static RuleResult Success() => new RuleResult(true);
        public static RuleResult Failure(string reason) => new RuleResult(false, reason);
    }

    /// <summary>
    /// Базовый интерфейс для правила drag-and-drop
    /// Каждое правило получает контекст (Target) и конкретный entry (Source/Stack)
    /// </summary>
    public interface IDragRule
    {
        /// <summary>
        /// Приоритет выполнения правила (меньше = раньше)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Отображаемое имя правила в Inspector
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// Проверить, можно ли начать перетаскивание
        /// </summary>
        RuleResult CanStartDrag(DragContext context, DragEntry entry);

        /// <summary>
        /// Проверить, можно ли бросить предмет в целевой слот
        /// </summary>
        RuleResult CanDrop(DragContext context, DragEntry entry);
    }

    /// <summary>
    /// Маркерный интерфейс для правил, применимых к глобальному менеджеру
    /// </summary>
    public interface IGlobalRule : IDragRule { }

    /// <summary>
    /// Маркерный интерфейс для правил, применимых к инвентарю
    /// </summary>
    public interface IInventoryRule : IDragRule { }

    /// <summary>
    /// Маркерный интерфейс для правил, применимых к конкретному слоту
    /// </summary>
    public interface ISlotRule : IDragRule { }

    /// <summary>
    /// Базовый класс для упрощения создания правил
    /// </summary>
    public abstract class DragRuleBase : IDragRule
    {
        public virtual int Priority => 100;

        // Для отображения в Odin Inspector
        public virtual string RuleName => $"[{Priority}] {GetType().Name.Replace("Rule", "")}";

        public virtual RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            return RuleResult.Success();
        }

        public virtual RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            return RuleResult.Success();
        }
    }
}
