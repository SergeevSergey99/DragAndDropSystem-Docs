using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Tools;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Drop processor for inventory-based targets (slots and inventory areas).
    /// Encapsulates 3-tier rule validation and delegates to the planner/executor pipeline.
    /// </summary>
    public class InventoryDropProcessor : IDropRequestProcessor
    {
        private readonly BaseSlot _targetBaseSlot;
        private readonly IInventory _targetInventory;
        private readonly DropRequestPolicy? _boundRequestOverride;
        private readonly GlobalRuleValidator _globalRules;
        private readonly TransferPlanner _planner;
        private readonly TransferPlanExecutor _executor;
        private readonly Func<InventorySwapContext, bool> _swapAttempting;
        private readonly Action<InventorySwapContext> _swapCompleted;
        public TransferExecutionSummary LastExecutionSummary { get; private set; }

        /// <summary>
        /// Create a processor for a specific slot
        /// </summary>
        public InventoryDropProcessor(
            BaseSlot targetBaseSlot,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            DropRequestPolicy? boundRequestOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null)
        {
            _targetBaseSlot = targetBaseSlot;
            _targetInventory = targetInventory;
            _boundRequestOverride = boundRequestOverride;
            _globalRules = globalRules;
            _planner = new TransferPlanner();
            _executor = new TransferPlanExecutor();
            _swapAttempting = swapAttempting;
            _swapCompleted = swapCompleted;
        }

        /// <summary>
        /// Create a processor for an inventory area (no specific slot)
        /// </summary>
        public InventoryDropProcessor(
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            DropRequestPolicy? boundRequestOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null)
            : this(null, targetInventory, globalRules, boundRequestOverride, swapAttempting, swapCompleted)
        {
        }

        public bool CanAcceptDrop(DragContext context)
        {
            return CanAcceptDrop(context, null);
        }

        public bool CanAcceptDrop(DragContext context, DropRequestPolicy? requested)
        {
            if (context == null || _targetInventory == null)
            {
                Extensions.DragAndDropLog("<color=red>[InventoryDropProcessor] CanAcceptDrop: null context or inventory</color>");
                return false;
            }

            var effectivePolicy = ResolveEffectivePolicy(context, requested);

            var plan = _planner.BuildPlan(
                context,
                effectivePolicy,
                _targetInventory,
                _targetBaseSlot,
                _globalRules);

            if (!plan.IsValid)
            {
                Extensions.DragAndDropLog($"<color=red>[InventoryDropProcessor] CanAcceptDrop: plan failed: {plan.Failure?.Reason}</color>");
                return false;
            }

            Extensions.DragAndDropLog("<color=green>[InventoryDropProcessor] CanAcceptDrop: plan is valid</color>");
            return true;
        }

        public DropResult ProcessDrop(DragContext context)
        {
            return ProcessDrop(context, null);
        }

        public DropResult ProcessDrop(DragContext context, DropRequestPolicy? requested)
        {
            var summary = ProcessDropWithSummary(context, requested);
            return summary.DropResult;
        }

        public TransferExecutionSummary ProcessDropWithSummary(DragContext context)
        {
            return ProcessDropWithSummary(context, null);
        }

        public TransferExecutionSummary ProcessDropWithSummary(DragContext context, DropRequestPolicy? requested)
        {
            if (!TryPrepareExecution(context, requested, "ProcessDrop", out var plan, out var failureSummary))
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
            DropRequestPolicy? requested = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryPrepareExecution(context, requested, "ProcessDropAsync", out var plan, out var failureSummary))
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
            DropRequestPolicy? requested,
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
            var sourceSlot = entry.SourceBaseSlot;
            var draggedStack = entry.Stack;

            if (source == null || sourceSlot == null || draggedStack == null)
            {
                Extensions.DragAndDropLog($"<color=red>[InventoryDropProcessor] {operationName}: Invalid context</color>");
                failureSummary = BuildFailureSummary("Invalid drag context");
                return false;
            }

            if (_targetInventory == null)
            {
                failureSummary = BuildFailureSummary("Target inventory is null");
                return false;
            }

            var effectivePolicy = ResolveEffectivePolicy(context, requested);

            plan = _planner.BuildPlan(
                context,
                effectivePolicy,
                _targetInventory,
                _targetBaseSlot,
                _globalRules);

            if (plan == null || !plan.IsValid)
            {
                failureSummary = BuildFailureSummary(plan?.Failure?.Reason ?? "Transfer plan is invalid");
                return false;
            }

            var policy = plan.Policy;
            var blockedResolverName = policy.BlockedTargetResolver?.GetType().Name ?? "None";
            Extensions.DragAndDropLog($"<color=yellow>[InventoryDropProcessor] {operationName}: {draggedStack.Count}x {draggedStack.DisplayName} | TargetSlot={_targetBaseSlot?.Index.ToString() ?? "AREA"} | Policy=[BlockedResolver={blockedResolverName}, Partial={policy.AllowPartial}, Batch={policy.BatchMode}]</color>");
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
                Extensions.DragAndDropLog($"<color=red>[InventoryDropProcessor] {failureLogPrefix}: {summary.DropResult.FailureReason}</color>");
                return summary;
            }

            if (summary.DropResult.TargetBaseSlot != null && summary.DropResult.TargetInventory != null)
            {
                context.SetTarget(summary.DropResult.TargetBaseSlot, summary.DropResult.TargetInventory);
            }

            Extensions.DragAndDropLog($"<color=green>[InventoryDropProcessor] {successLogPrefix}: amount={summary.TransferredAmount}, successEntries={summary.SucceededEntries}, failedEntries={summary.FailedEntries}</color>");
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

        private ResolvedDropPolicy ResolveEffectivePolicy(DragContext context, DropRequestPolicy? requested)
        {
            requested = DropRequestPolicy.Merge(_boundRequestOverride, requested);

            var provider = _targetInventory as IDropPolicyProvider;
            if (provider != null)
                return provider.ResolveDropPolicy(requested, context);

            var blockedTargetResolver = requested.HasValue && requested.Value.BlockedTargetResolver != null
                ? requested.Value.BlockedTargetResolver
                : new FindAlternativeBlockedTargetResolver();
            var allowPartial = requested.HasValue && requested.Value.AllowPartial.HasValue
                ? requested.Value.AllowPartial.Value
                : true;

            return new ResolvedDropPolicy(blockedTargetResolver, allowPartial, BatchMode.BestEffort);
        }
    }
}
