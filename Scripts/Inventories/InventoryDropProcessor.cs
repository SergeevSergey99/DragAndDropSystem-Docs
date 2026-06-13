using System;
using System.Threading;
using System.Threading.Tasks;
using UDND.Core;
using UDND.Rules;
using UDND.Slots;
using UDND.Tools;

namespace UDND.Inventories
{
    /// <summary>
    /// Drop processor for inventory-based targets (slots and inventory areas).
    /// Uses a read-only acceptance probe and delegates mutation to the JIT transfer service.
    /// </summary>
    public class InventoryDropProcessor : IDropRequestProcessor
    {
        private readonly BaseSlot _targetBaseSlot;
        private readonly IInventory _targetInventory;
        private readonly DropRequestPolicy? _boundRequestOverride;
        private readonly GlobalRuleValidator _globalRules;
        private readonly InventoryTransferService _jitService;
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
            _jitService = new InventoryTransferService();
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

            bool canAttempt = _jitService.CanAttempt(
                context,
                _targetInventory,
                _targetBaseSlot,
                effectivePolicy,
                _globalRules);

            if (!canAttempt)
            {
                Extensions.DragAndDropLog("<color=red>[InventoryDropProcessor] CanAcceptDrop: probe rejected</color>");
                return false;
            }

            Extensions.DragAndDropLog("<color=green>[InventoryDropProcessor] CanAcceptDrop: probe accepted</color>");
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
            if (context == null)
            {
                var fail = BuildFailureSummary("Null drag context");
                LastExecutionSummary = fail;
                return fail;
            }

            var effectivePolicy = ResolveEffectivePolicy(context, requested);
            var report = _jitService.ExecuteBatch(
                context,
                _targetInventory,
                _targetBaseSlot,
                effectivePolicy,
                _swapAttempting,
                _swapCompleted,
                _globalRules);

            var summary = report.ToExecutionSummary(_targetInventory);
            return FinalizeExecution(context, summary, "JIT execute failed", "JIT executed");
        }

        public Task<TransferExecutionSummary> ProcessDropWithSummaryAsync(
            DragContext context,
            DropRequestPolicy? requested = null,
            CancellationToken cancellationToken = default)
        {
            // JIT service is synchronous; return a completed task for API compatibility.
            return Task.FromResult(ProcessDropWithSummary(context, requested));
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

            return new ResolvedDropPolicy(
                requested?.BlockedTargetResolution ??
                BlockedTargetResolutionKind.AlternativeSlots,
                requested?.AlternativeOrderer ??
                MergeFirstPlacementCandidateOrderer.Instance,
                requested?.AllowSameInventoryAlternativePlacement ?? true,
                requested?.PartialTransferMode ?? PartialTransferMode.Allow);
        }
    }
}
