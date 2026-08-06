using System.Collections.Generic;
using UDND.Slots;
using UDND.Core;
using UDND.Inventories;

namespace UDND.Rules
{
    /// <summary>
    /// Single rule evaluation entry point for transfer services and handlers.
    /// <para>
    /// Domain boundary: start rules see the item in the source domain, drop rules see it in the
    /// target domain. Conversion happens here, once per entry, before any drop rule runs — a
    /// target must never be asked to judge an item shaped for somebody else's inventory.
    /// </para>
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

            if (entry.SourceInventory == null || entry.SourceBaseSlot == null || entry.Stack == null || entry.Stack.PrimaryAdapter == null)
                return RuleResult.Failure("Invalid source entry");

            if (globalRules != null)
            {
                var globalStartResult = globalRules.ValidateStartDrag(context, entry);
                if (!globalStartResult.IsValid)
                    return globalStartResult;
            }

            if (entry.SourceInventory is IInventoryRuleEvaluator sourceRuleEvaluator)
            {
                var sourceResult = sourceRuleEvaluator.RuleValidator.ValidateStartDrag(context, entry);
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

            // An item that cannot cross into the target domain cannot be dropped there. Reporting
            // it here — instead of letting the rules pass and failing at mutation time — is what
            // keeps the drop preview honest.
            if (!TryResolveTargetDomain(context, entry, out var targetContext, out var targetEntry))
                return RuleResult.Failure("Item cannot be converted for the target inventory");

            if (globalRules != null)
            {
                var globalDropResult = globalRules.ValidateDrop(targetContext, targetEntry);
                if (!globalDropResult.IsValid)
                    return globalDropResult;
            }

            if (targetContext.TargetInventory is IInventoryRuleEvaluator targetRuleEvaluator)
            {
                var inventoryDropResult = targetRuleEvaluator.RuleValidator.ValidateDrop(targetContext, targetEntry);
                if (!inventoryDropResult.IsValid)
                    return inventoryDropResult;
            }

            var targetBinding = targetContext.TargetInventory.DataBinding;
            if (targetBinding != null)
            {
                var bindingDropResult = targetBinding.ValidateDropRules(targetContext, targetEntry);
                if (!bindingDropResult.IsValid)
                    return bindingDropResult;
            }

            if (targetContext.TargetBaseSlot?.SlotRuleValidator != null)
            {
                var slotDropResult = targetContext.TargetBaseSlot.SlotRuleValidator.ValidateDrop(targetContext, targetEntry);
                if (!slotDropResult.IsValid)
                    return slotDropResult;
            }

            return RuleResult.Success();
        }

        /// <summary>
        /// Produces the target-domain view of the validated entry.
        /// <para>
        /// Conversion is resolved through the drag's conversion session, so the object the rules
        /// judge is the object the transfer will later commit. Only the entry under validation is
        /// converted: the remaining batch entries keep their source domain until their own turn,
        /// which is where their own target is known.
        /// </para>
        /// </summary>
        private static bool TryResolveTargetDomain(
            DragContext context,
            DragEntry entry,
            out DragContext targetContext,
            out DragEntry targetEntry)
        {
            targetContext = context;
            targetEntry = entry;

            var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
            var targetInventory = context.TargetInventory;
            var stack = entry.Stack;

            // No source inventory means the entry carries no source domain to convert from: the
            // caller already handed us a target-domain item (generic acceptance queries, prefab
            // rule checks, code-driven adds).
            if (sourceInventory == null ||
                ReferenceEquals(sourceInventory, targetInventory) ||
                stack == null ||
                stack.IsEmpty ||
                stack.PrimaryAdapter == null)
                return true;

            // Every instance is resolved, not just the primary one: a converter may legitimately
            // pass some items through unchanged and rebuild others, and inferring "nothing to do"
            // from the first adapter would hand the rules a half-converted stack.
            var session = context.ConversionSession;
            var sourceAdapters = stack.Adapters;
            List<IItemAdapter> convertedAdapters = null;
            for (int i = 0; i < sourceAdapters.Count; i++)
            {
                if (!TransferItemConversionUtility.TryResolveTargetItem(
                        sourceInventory,
                        targetInventory,
                        sourceAdapters[i],
                        session,
                        out var converted))
                    return false;

                if (convertedAdapters == null && ReferenceEquals(converted, sourceAdapters[i]))
                    continue;

                if (convertedAdapters == null)
                {
                    // First item that actually changed: materialize what we skipped so far.
                    convertedAdapters = new List<IItemAdapter>(sourceAdapters.Count);
                    for (int j = 0; j < i; j++)
                        convertedAdapters.Add(sourceAdapters[j]);
                }

                convertedAdapters.Add(converted);
            }

            // Nothing changed at this boundary: reuse the entry as-is so same-domain drags stay
            // allocation-free.
            if (convertedAdapters == null)
                return true;

            if (!ItemStack.TryCreate(convertedAdapters, out var convertedStack))
                return false;

            targetEntry = new DragEntry(
                convertedStack,
                entry.SourceBaseSlot,
                entry.SourceInventory,
                entry.SourcePlacement,
                entry.GrabOffset,
                entry.Orientation,
                entry.OrientationTopology);
            targetContext = ReplaceEntry(context, entry, targetEntry);
            return true;
        }

        /// <summary>
        /// Copy of the context with the validated entry swapped for its target-domain view,
        /// keeping the position and the rest of a batch intact.
        /// </summary>
        private static DragContext ReplaceEntry(DragContext context, DragEntry original, DragEntry replacement)
        {
            var entries = context.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!ReferenceEquals(entries[i].Stack, original.Stack))
                        continue;

                    var replaced = new List<DragEntry>(entries.Count);
                    for (int j = 0; j < entries.Count; j++)
                        replaced.Add(j == i ? replacement : entries[j]);

                    return context.WithEntries(replaced);
                }
            }

            return context.WithEntries(new[] { replacement });
        }
    }
}
