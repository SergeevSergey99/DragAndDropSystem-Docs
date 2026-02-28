using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Rules
{
    /// <summary>
    /// Единая точка применения правил для planner/executor/handlers.
    /// </summary>
    public class RuleEvaluationService
    {
        public RuleResult ValidateEntryStart(
            DragContext context,
            DragEntry entry,
            GlobalRuleValidator globalRules = null)
        {
            if (context == null)
                return RuleResult.Failure("Drag context is null");

            if (entry.SourceInventory == null || entry.SourceSlot == null || entry.Stack == null || entry.Stack.Item == null)
                return RuleResult.Failure("Invalid source entry");

            if (globalRules != null)
            {
                var globalStartResult = globalRules.ValidateStartDrag(context, entry);
                if (!globalStartResult.IsValid)
                    return globalStartResult;
            }

            if (entry.SourceInventory is UniversalInventory sourceUniversal)
            {
                var sourceResult = sourceUniversal.RuleValidator.ValidateStartDrag(context, entry);
                if (!sourceResult.IsValid)
                    return sourceResult;
            }

            return RuleResult.Success();
        }

        public RuleResult ValidateEntryDrop(
            DragContext context,
            DragEntry entry,
            IInventory targetInventory,
            ISlot targetSlot,
            GlobalRuleValidator globalRules = null)
        {
            if (context == null)
                return RuleResult.Failure("Drag context is null");

            if (targetInventory == null)
                return RuleResult.Failure("Target inventory is null");

            var previousSlot = context.TargetSlot;
            var previousInventory = context.TargetInventory;
            context.SetTarget(targetSlot, targetInventory);

            try
            {
                if (globalRules != null)
                {
                    var globalDropResult = globalRules.ValidateDrop(context, entry);
                    if (!globalDropResult.IsValid)
                        return globalDropResult;
                }

                if (targetInventory is UniversalInventory targetUniversal)
                {
                    var inventoryDropResult = targetUniversal.RuleValidator.ValidateDrop(context, entry);
                    if (!inventoryDropResult.IsValid)
                        return inventoryDropResult;
                }

                if (targetSlot?.SlotRuleValidator != null)
                {
                    var slotDropResult = targetSlot.SlotRuleValidator.ValidateDrop(context, entry);
                    if (!slotDropResult.IsValid)
                        return slotDropResult;
                }

                return RuleResult.Success();
            }
            finally
            {
                context.SetTarget(previousSlot, previousInventory);
            }
        }
    }
}
