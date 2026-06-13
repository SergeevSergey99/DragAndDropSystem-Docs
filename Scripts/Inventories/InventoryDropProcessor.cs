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
        public TransferExecutionReport LastExecutionReport { get; private set; }
        public TransferProbe LastProbe { get; private set; }

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
                LastProbe = TransferProbe.Rejected("Null drag context or target inventory");
                Extensions.DragAndDropLog("<color=red>[InventoryDropProcessor] CanAcceptDrop: null context or inventory</color>");
                return false;
            }

            var effectivePolicy = ResolveEffectivePolicy(context, requested);

            LastProbe = _jitService.Probe(
                context,
                _targetInventory,
                _targetBaseSlot,
                effectivePolicy,
                _globalRules);

            if (!LastProbe.CanAttempt)
            {
                Extensions.DragAndDropLog(
                    $"<color=red>[InventoryDropProcessor] CanAcceptDrop: probe rejected: {LastProbe.FailureReason}</color>");
                return false;
            }

            Extensions.DragAndDropLog("<color=green>[InventoryDropProcessor] CanAcceptDrop: probe accepted</color>");
            return true;
        }

        public DropResult ProcessDrop(DragContext context)
            => ProcessDrop(context, null);

        public DropResult ProcessDrop(DragContext context, DropRequestPolicy? requested)
            => ProcessDropWithReport(context, requested).ToDropResult(_targetInventory);

        public TransferExecutionReport ProcessDropWithReport(DragContext context)
            => ProcessDropWithReport(context, null);

        public TransferExecutionReport ProcessDropWithReport(DragContext context, DropRequestPolicy? requested)
        {
            if (context == null)
            {
                var fail = TransferExecutionReport.Rejected("Null drag context");
                LastExecutionReport = fail;
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

            return FinalizeExecution(context, report, "JIT execute failed", "JIT executed");
        }

        public async Task<TransferExecutionReport> ProcessDropWithReportAsync(
            DragContext context,
            DropRequestPolicy? requested = null,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                var fail = TransferExecutionReport.Rejected("Null drag context");
                LastExecutionReport = fail;
                return fail;
            }

            var effectivePolicy = ResolveEffectivePolicy(context, requested);
            var report = await _jitService.ExecuteBatchAsync(
                context,
                _targetInventory,
                _targetBaseSlot,
                effectivePolicy,
                _swapAttempting,
                _swapCompleted,
                _globalRules,
                cancellationToken);

            return FinalizeExecution(context, report, "Async JIT execute failed", "Async JIT executed");
        }

        private TransferExecutionReport FinalizeExecution(
            DragContext context,
            TransferExecutionReport report,
            string failureLogPrefix,
            string successLogPrefix)
        {
            LastExecutionReport = report;
            if (!report.Success)
            {
                string reason = report.FailureReason;
                if (string.IsNullOrEmpty(reason))
                    foreach (var e in report.EntryResults)
                        if (!string.IsNullOrEmpty(e.FailureReason)) { reason = e.FailureReason; break; }
                Extensions.DragAndDropLog($"<color=red>[InventoryDropProcessor] {failureLogPrefix}: {reason}</color>");
                return report;
            }

            // Set the last-placed slot as the context target for post-drop feedback.
            BaseSlot lastTargetSlot = null;
            foreach (var e in report.EntryResults)
                foreach (var o in e.Outcomes)
                    lastTargetSlot = o.TargetBaseSlot;
            if (lastTargetSlot != null && _targetInventory != null)
                context.SetTarget(lastTargetSlot, _targetInventory);

            Extensions.DragAndDropLog($"<color=green>[InventoryDropProcessor] {successLogPrefix}: amount={report.TransferredAmount}, successEntries={report.SucceededEntries}, failedEntries={report.FailedEntries}</color>");
            return report;
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
                requested?.AlternativeOrderer,
                requested?.AllowSameInventoryAlternativePlacement ?? true,
                requested?.PartialTransferMode ?? PartialTransferMode.Allow);
        }
    }
}
