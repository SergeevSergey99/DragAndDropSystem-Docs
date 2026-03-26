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

            var sourceBinding = entry.SourceInventory.DataBinding;
            if (sourceBinding != null)
            {
                var bindingResult = sourceBinding.ValidateStartDragRules(context, entry);
                if (!bindingResult.IsValid)
                    return bindingResult;
            }

            return RuleResult.Success();
        }

        public RuleResult ValidateEntryDrop(
            DragContext context,
            DragEntry entry,
            GlobalRuleValidator globalRules = null)
        {
            if (context == null)
                return RuleResult.Failure("Drag context is null");

            if (context.TargetInventory == null)
                return RuleResult.Failure("Target inventory is null");

            if (globalRules != null)
            {
                var globalDropResult = globalRules.ValidateDrop(context, entry);
                if (!globalDropResult.IsValid)
                    return globalDropResult;
            }

            if (context.TargetInventory is UniversalInventory targetUniversal)
            {
                var inventoryDropResult = targetUniversal.RuleValidator.ValidateDrop(context, entry);
                if (!inventoryDropResult.IsValid)
                    return inventoryDropResult;
            }

            var targetBinding = context.TargetInventory.DataBinding;
            if (targetBinding != null)
            {
                var bindingDropResult = targetBinding.ValidateDropRules(context, entry);
                if (!bindingDropResult.IsValid)
                    return bindingDropResult;
            }

            if (context.TargetSlot?.SlotRuleValidator != null)
            {
                var slotDropResult = context.TargetSlot.SlotRuleValidator.ValidateDrop(context, entry);
                if (!slotDropResult.IsValid)
                    return slotDropResult;
            }

            return RuleResult.Success();
        }
    }
}
