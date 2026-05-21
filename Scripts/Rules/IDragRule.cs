using UnityEngine;
using UDND.Core;
using UDND.Tools;

namespace UDND.Rules
{
    /// <summary>
    /// Rule validation result
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
                Extensions.DragAndDropLogWarning($"<color=red>[RuleResult] Validation failed: {failureReason}</color>");
            }
        }

        public static RuleResult Success() => new RuleResult(true);
        public static RuleResult Failure(string reason) => new RuleResult(false, reason);
    }

    /// <summary>
    /// Base interface for a drag-and-drop rule
    /// Each rule receives the context (Target) and a specific entry (Source/Stack)
    /// </summary>
    public interface IDragRule
    {
        /// <summary>
        /// Rule execution priority (lower = earlier)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Display name of the rule in the Inspector
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// Check whether dragging can start
        /// </summary>
        RuleResult CanStartDrag(DragContext context, DragEntry entry);

        /// <summary>
        /// Check whether the item can be dropped into the target slot
        /// </summary>
        RuleResult CanDrop(DragContext context, DragEntry entry);
    }

    /// <summary>
    /// Marker interface for rules applicable to the global manager
    /// </summary>
    public interface IGlobalRule : IDragRule { }

    /// <summary>
    /// Marker interface for rules applicable to an inventory
    /// </summary>
    public interface IInventoryRule : IDragRule { }

    /// <summary>
    /// Marker interface for rules applicable to a specific slot
    /// </summary>
    public interface ISlotRule : IDragRule { }

    /// <summary>
    /// Base class that simplifies rule creation
    /// </summary>
    public abstract class DragRuleBase : IDragRule
    {
        public virtual int Priority => 100;

        // Used for display in Odin Inspector
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