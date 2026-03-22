using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Drop processor for inventory-based targets (slots and inventory areas).
    /// Encapsulates 3-tier rule validation and delegates to the planner/executor pipeline.
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
            DropPolicy policyOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null)
        {
            _targetSlot = targetSlot;
            _targetInventory = targetInventory;
            _globalRules = globalRules;
            _planner = new TransferPlanner();
            _executor = new TransferPlanExecutor();
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
            DropPolicy policyOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null)
            : this(null, targetInventory, globalRules, policyOverride, swapAttempting, swapCompleted)
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
            if (!TryPrepareExecution(context, "ProcessDrop", out var plan, out var failureSummary))
            {
                LastExecutionSummary = failureSummary;
                return failureSummary;
            }

            var summary = _executor.Execute(plan, new TransferExecutionOptions
            {
                GlobalRules = _globalRules,
                SwapAttempting = _swapAttempting,
                SwapCompleted = _swapCompleted
            });
            return FinalizeExecution(context, summary, "Execute failed", "Executed plan");
        }

        public async Task<TransferExecutionSummary> ProcessDropWithSummaryAsync(
            DragContext context,
            CancellationToken cancellationToken = default)
        {
            if (!TryPrepareExecution(context, "ProcessDropAsync", out var plan, out var failureSummary))
            {
                LastExecutionSummary = failureSummary;
                return failureSummary;
            }

            var summary = await _executor.ExecuteAsync(plan, new TransferExecutionOptions
            {
                GlobalRules = _globalRules,
                SwapAttempting = _swapAttempting,
                SwapCompleted = _swapCompleted
            }, cancellationToken);
            return FinalizeExecution(context, summary, "ExecuteAsync failed", "Executed async plan");
        }

        private bool TryPrepareExecution(
            DragContext context,
            string operationName,
            out TransferPlan plan,
            out TransferExecutionSummary failureSummary)
        {
            plan = null;
            failureSummary = null;

            if (context == null)
            {
                failureSummary = BuildFailureSummary("Null drag context");
                return false;
            }

            if (context.Entries == null || context.Entries.Count == 0)
            {
                failureSummary = BuildFailureSummary("Drag context has no entries");
                return false;
            }

            var entry = context.Entries[0];
            var source = entry.SourceInventory;
            var sourceSlot = entry.SourceSlot;
            var draggedStack = entry.Stack;

            if (source == null || sourceSlot == null || draggedStack == null)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryDropProcessor] {operationName}: Invalid context</color>");
                failureSummary = BuildFailureSummary("Invalid drag context");
                return false;
            }

            if (_targetInventory == null)
            {
                failureSummary = BuildFailureSummary("Target inventory is null");
                return false;
            }

            var effectivePolicy = ResolveEffectivePolicy(context);
            context.Policy = effectivePolicy;

            plan = _cachedPlan ?? _planner.BuildPlan(
                context,
                effectivePolicy,
                _targetInventory,
                _targetSlot,
                _globalRules);
            _cachedPlan = null;

            if (plan == null || !plan.IsValid)
            {
                failureSummary = BuildFailureSummary(plan?.Failure?.Reason ?? "Transfer plan is invalid");
                return false;
            }

            var policy = context.Policy;
            Extentions.DragAndDropLog($"<color=yellow>[InventoryDropProcessor] {operationName}: {draggedStack.Count}x {draggedStack.Item.DisplayName} | TargetSlot={_targetSlot?.Index.ToString() ?? "AREA"} | Policy=[Target={policy?.TargetUsage}, Occupied={policy?.OccupiedTarget}, Capacity={policy?.Capacity}, Batch={policy?.BatchExecution}]</color>");
            return true;
        }

        private TransferExecutionSummary FinalizeExecution(
            DragContext context,
            TransferExecutionSummary summary,
            string failureLogPrefix,
            string successLogPrefix)
        {
            LastExecutionSummary = summary;
            if (!summary.Success)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryDropProcessor] {failureLogPrefix}: {summary.DropResult.FailureReason}</color>");
                return summary;
            }

            if (summary.DropResult.TargetSlot != null && summary.DropResult.TargetInventory != null)
            {
                context.SetTarget(summary.DropResult.TargetSlot, summary.DropResult.TargetInventory);
            }

            Extentions.DragAndDropLog($"<color=green>[InventoryDropProcessor] {successLogPrefix}: amount={summary.TransferredAmount}, successEntries={summary.SucceededEntries}, failedEntries={summary.FailedEntries}</color>");
            return summary;
        }

        private static TransferExecutionSummary BuildFailureSummary(string reason)
        {
            return new TransferExecutionSummary(
                success: false,
                succeededEntries: 0,
                failedEntries: 0,
                transferredAmount: 0,
                isPartial: false,
                dropResult: DropResult.Failed(reason));
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
