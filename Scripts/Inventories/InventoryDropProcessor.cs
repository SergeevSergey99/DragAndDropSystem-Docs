using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using System;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Drop processor for inventory-based targets (slots and inventory areas).
    /// Encapsulates 3-tier rule validation and delegates to InventoryTransferService.
    /// </summary>
    public class InventoryDropProcessor : IDropProcessor
    {
        private readonly ISlot _targetSlot;
        private readonly IInventory _targetInventory;
        private readonly GlobalRuleValidator _globalRules;
        private readonly TransferPlanner _planner;
        private readonly TransferPlanExecutor _executor;
        private readonly DropPolicy _policyOverride;
        private readonly Func<InventorySwapContext, bool> _swapAttempting;
        private readonly Action<InventorySwapContext> _swapCompleted;
        private TransferPlan _cachedPlan;
        public TransferExecutionSummary LastExecutionSummary { get; private set; }

        /// <summary>
        /// Create a processor for a specific slot
        /// </summary>
        public InventoryDropProcessor(
            ISlot targetSlot,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            InventoryTransferService transferService,
            DropPolicy policyOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null)
        {
            _targetSlot = targetSlot;
            _targetInventory = targetInventory;
            _globalRules = globalRules;
            _planner = new TransferPlanner();
            _executor = new TransferPlanExecutor(transferService);
            _policyOverride = policyOverride;
            _swapAttempting = swapAttempting;
            _swapCompleted = swapCompleted;
        }

        /// <summary>
        /// Create a processor for an inventory area (no specific slot)
        /// </summary>
        public InventoryDropProcessor(
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            InventoryTransferService transferService,
            DropPolicy policyOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null)
            : this(null, targetInventory, globalRules, transferService, policyOverride, swapAttempting, swapCompleted)
        {
        }

        public bool CanAcceptDrop(DragContext context)
        {
            if (context == null || _targetInventory == null)
            {
                Extentions.DragAndDropLog("<color=red>[InventoryDropProcessor] CanAcceptDrop: null context or inventory</color>");
                _cachedPlan = null;
                return false;
            }

            var effectivePolicy = ResolveEffectivePolicy(context);
            context.Policy = effectivePolicy;

            var plan = _planner.BuildPlan(
                context,
                effectivePolicy,
                _targetInventory,
                _targetSlot,
                _globalRules);

            _cachedPlan = plan.IsValid ? plan : null;
            if (!plan.IsValid)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryDropProcessor] CanAcceptDrop: plan failed: {plan.Failure?.Reason}</color>");
                return false;
            }

            Extentions.DragAndDropLog("<color=green>[InventoryDropProcessor] CanAcceptDrop: plan is valid</color>");
            return true;
        }

        public DropResult ProcessDrop(DragContext context)
        {
            var summary = ProcessDropWithSummary(context);
            return summary.DropResult;
        }

        public TransferExecutionSummary ProcessDropWithSummary(DragContext context)
        {
            if (context == null)
            {
                LastExecutionSummary = new TransferExecutionSummary(
                    success: false,
                    succeededEntries: 0,
                    failedEntries: 0,
                    transferredAmount: 0,
                    isPartial: false,
                    dropResult: DropResult.Failed("Null drag context"));
                return LastExecutionSummary;
            }
            if (context.Entries == null || context.Entries.Count == 0)
            {
                LastExecutionSummary = new TransferExecutionSummary(
                    success: false,
                    succeededEntries: 0,
                    failedEntries: 0,
                    transferredAmount: 0,
                    isPartial: false,
                    dropResult: DropResult.Failed("Drag context has no entries"));
                return LastExecutionSummary;
            }

            var entry = context.Entries[0];
            var source = entry.SourceInventory;
            var sourceSlot = entry.SourceSlot;
            var draggedStack = entry.Stack;

            if (source == null || sourceSlot == null || draggedStack == null)
            {
                Extentions.DragAndDropLog("<color=red>[InventoryDropProcessor] ProcessDrop: Invalid context</color>");
                LastExecutionSummary = new TransferExecutionSummary(
                    success: false,
                    succeededEntries: 0,
                    failedEntries: 0,
                    transferredAmount: 0,
                    isPartial: false,
                    dropResult: DropResult.Failed("Invalid drag context"));
                return LastExecutionSummary;
            }

            if (_targetInventory == null)
            {
                LastExecutionSummary = new TransferExecutionSummary(
                    success: false,
                    succeededEntries: 0,
                    failedEntries: 0,
                    transferredAmount: 0,
                    isPartial: false,
                    dropResult: DropResult.Failed("Target inventory is null"));
                return LastExecutionSummary;
            }

            var effectivePolicy = ResolveEffectivePolicy(context);
            context.Policy = effectivePolicy;

            var plan = _cachedPlan ?? _planner.BuildPlan(
                context,
                effectivePolicy,
                _targetInventory,
                _targetSlot,
                _globalRules);
            _cachedPlan = null;

            if (plan == null || !plan.IsValid)
            {
                LastExecutionSummary = new TransferExecutionSummary(
                    success: false,
                    succeededEntries: 0,
                    failedEntries: 0,
                    transferredAmount: 0,
                    isPartial: false,
                    dropResult: DropResult.Failed(plan?.Failure?.Reason ?? "Transfer plan is invalid"));
                return LastExecutionSummary;
            }

            var policy = context.Policy;
            Extentions.DragAndDropLog($"<color=yellow>[InventoryDropProcessor] ProcessDrop: {draggedStack.Count}x {draggedStack.Item.DisplayName} | TargetSlot={_targetSlot?.Index.ToString() ?? "AREA"} | Policy=[Target={policy?.TargetUsage}, Occupied={policy?.OccupiedTarget}, Capacity={policy?.Capacity}, Batch={policy?.BatchExecution}]</color>");

            var summary = _executor.Execute(plan, new TransferExecutionOptions
            {
                GlobalRules = _globalRules,
                SwapAttempting = _swapAttempting,
                SwapCompleted = _swapCompleted
            });
            LastExecutionSummary = summary;
            if (!summary.Success)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryDropProcessor] Execute failed: {summary.DropResult.FailureReason}</color>");
                return summary;
            }

            if (summary.DropResult.TargetSlot != null && summary.DropResult.TargetInventory != null)
            {
                context.SetTarget(summary.DropResult.TargetSlot, summary.DropResult.TargetInventory);
            }

            Extentions.DragAndDropLog($"<color=green>[InventoryDropProcessor] Executed plan: amount={summary.TransferredAmount}, successEntries={summary.SucceededEntries}, failedEntries={summary.FailedEntries}</color>");
            return summary;
        }

        private DropPolicy ResolveEffectivePolicy(DragContext context)
        {
            if (_policyOverride != null)
                return _policyOverride;

            if (_targetInventory is UniversalInventory universalInventory)
                return universalInventory.GetDropPolicy(context?.IsBatchDrag ?? false);

            if (context?.Policy != null)
                return context.Policy;

            return DropPolicy.SingleDefault;
        }
    }
}
