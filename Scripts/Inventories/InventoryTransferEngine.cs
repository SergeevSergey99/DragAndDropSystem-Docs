using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Rules;
using UDND.Slots;
using UDND.Tools;

namespace UDND.Inventories
{
    /// <summary>
    /// Input for a single-entry JIT transfer (plan: Unified Placement Transfer, stage 3).
    /// </summary>
    public sealed class TransferEntryRequest
    {
        public TransferEntryRequest(
            DragContext context,
            DragEntry entry,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ResolvedDropPolicy policy,
            PlacementCandidateOrderer ordererOverride = null,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null,
            GlobalRuleValidator globalRules = null)
        {
            Context = context;
            Entry = entry;
            TargetInventory = targetInventory;
            TargetBaseSlot = targetBaseSlot;
            Policy = policy;
            OrdererOverride = ordererOverride;
            SwapAttempting = swapAttempting;
            SwapCompleted = swapCompleted;
            GlobalRules = globalRules;
        }

        public DragContext Context { get; }
        public DragEntry Entry { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlot { get; }
        public ResolvedDropPolicy Policy { get; }
        public PlacementCandidateOrderer OrdererOverride { get; }
        public Func<InventorySwapContext, bool> SwapAttempting { get; }
        public Action<InventorySwapContext> SwapCompleted { get; }
        public GlobalRuleValidator GlobalRules { get; }
    }

    /// <summary>
    /// JIT transfer service: resolves candidates against the real inventory state and mutates it
    /// directly through narrow placement primitives. No materialized plan, no virtual occupancy.
    /// Each entry is its own transaction: a failed entry restores source and target snapshots,
    /// events/DataBinding notifications are dispatched only after the entry commits.
    /// </summary>
    public class InventoryTransferService
    {
        private sealed class CommittedOutcome
        {
            public PlacementTransferOutcome Outcome;
            public TransferDomainContext DomainContext;
        }

        private sealed class EntryTransaction
        {
            public IInventory SourceInventory;
            public IInventory TargetInventory;
            public BaseSlot SourceBaseSlot;
            public IInventorySnapshotProvider SourceSnapshotProvider;
            public IInventorySnapshotProvider TargetSnapshotProvider;
            public InventorySnapshot SourceSnapshot;
            public InventorySnapshot TargetSnapshot;
            public PlacementSnapshot SourcePlacementSnapshot;
            public IItemAdapter PreviewTargetItemAdapter;
            public int RequestedAmount;
            public int Remaining;
            public bool Aborted;
            public List<CommittedOutcome> Committed = new List<CommittedOutcome>();
        }

        /// <summary>
        /// Advisory read-only acceptance probe. It checks whether at least one entry currently has
        /// a viable first candidate; execution remains authoritative and revalidates everything.
        /// </summary>
        public bool CanAttempt(
            DragContext context,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ResolvedDropPolicy policy,
            GlobalRuleValidator globalRules = null)
        {
            if (context?.Entries == null || context.Entries.Count == 0 || targetInventory == null)
                return false;

            if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap &&
                context.Entries.Count > 1)
                return false;

            var validationContext = context.WithTarget(targetBaseSlot, targetInventory);
            if (!ValidateTransferStart(validationContext, targetInventory, out _))
                return false;

            var strategy = (targetInventory as IPlacementInventory)?.PlacementStrategy;
            if (strategy == null)
                return false;

            var geometry = new InventoryPlacementGeometry(targetInventory);
            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                var entryTargetSlot = i == 0 ? targetBaseSlot : null;
                var entryContext = context.WithTarget(entryTargetSlot, targetInventory);
                var rules = new RuleEvaluationService().ValidateEntryDrop(entryContext, entry, globalRules);
                if (!rules.IsValid || sourceInventory == null || entry.Stack?.PrimaryAdapter == null)
                    continue;

                if (entryTargetSlot != null && !entryTargetSlot.IsEmpty &&
                    targetInventory is IOccupiedSlotDropHandler occupiedHandler &&
                    occupiedHandler.CheckOccupiedSlotDrop(entry, entryTargetSlot))
                    return true;

                if (!TryResolvePreviewAdapter(
                        sourceInventory,
                        targetInventory,
                        entry.Stack.PrimaryAdapter,
                        out var previewAdapter))
                    continue;

                var acceptance = new InventoryAcceptanceRequest(
                    targetInventory,
                    previewAdapter,
                    entry.Stack.Count,
                    entryContext,
                    entry);

                if (entryTargetSlot != null)
                {
                    if (strategy.TryGetCandidate(geometry, acceptance, entryTargetSlot, out _))
                        return true;

                    if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Reject)
                        continue;

                    if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap)
                        return !entryTargetSlot.IsEmpty;

                    if (ReferenceEquals(sourceInventory, targetInventory) &&
                        !policy.AllowSameInventoryAlternativePlacement)
                        continue;
                }

                var orderer = entryTargetSlot != null
                    ? policy.AlternativeOrderer ?? MergeFirstPlacementCandidateOrderer.Instance
                    : strategy.DefaultOrderer ?? NaturalPlacementCandidateOrderer.Instance;
                foreach (var candidate in orderer.Order(strategy.GetCandidates(geometry, acceptance), acceptance))
                {
                    if (!ShouldSkipProbeCandidate(candidate, entry, sourceInventory, targetInventory, geometry))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sequential best-effort batch: each entry is its own transaction.
        /// Events/DataBinding commit per entry so the next entry sees the real state.
        /// Batch + Swap is rejected before any mutation.
        /// </summary>
        public TransferExecutionReport ExecuteBatch(
            DragContext context,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ResolvedDropPolicy policy,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null,
            GlobalRuleValidator globalRules = null)
        {
            if (context?.Entries == null || context.Entries.Count == 0)
                return TransferExecutionReport.Rejected("Empty drag context");

            if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap &&
                context.Entries.Count > 1)
                return TransferExecutionReport.Rejected("Swap requires a single full entry");

            var validationContext = context.WithTarget(targetBaseSlot, targetInventory);
            if (!ValidateTransferStart(validationContext, targetInventory, out var rejectionReason))
                return TransferExecutionReport.Rejected(rejectionReason);

            var results = new List<EntryTransferResult>(context.Entries.Count);
            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entryTargetSlot = i == 0 ? targetBaseSlot : null;
                var req = new TransferEntryRequest(
                    context, context.Entries[i], targetInventory, entryTargetSlot, policy,
                    swapAttempting: swapAttempting, swapCompleted: swapCompleted,
                    globalRules: globalRules);
                results.Add(TryTransferEntry(req));
            }

            return new TransferExecutionReport(results);
        }

        public EntryTransferResult TryTransferEntry(TransferEntryRequest request)
        {
            if (request?.Entry.Stack == null || request.Entry.Stack.IsEmpty)
                return EntryTransferResult.Failed(0, "Invalid entry");

            var entry = request.Entry;
            int requestedAmount = entry.Stack.Count;
            var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
            var targetInventory = request.TargetInventory;

            if (sourceInventory == null || entry.SourceBaseSlot == null)
                return EntryTransferResult.Failed(requestedAmount, "Invalid source");
            if (targetInventory == null)
                return EntryTransferResult.Failed(requestedAmount, "Target inventory is null");

            var validationContext = request.Context.WithTarget(request.TargetBaseSlot, targetInventory);
            var ruleResult = new RuleEvaluationService()
                .ValidateEntryDrop(validationContext, entry, request.GlobalRules);
            if (!ruleResult.IsValid)
            {
                return EntryTransferResult.Failed(
                    requestedAmount,
                    string.IsNullOrEmpty(ruleResult.FailureReason)
                        ? "Drop rules rejected the entry"
                        : ruleResult.FailureReason);
            }

            if (request.TargetBaseSlot != null &&
                !request.TargetBaseSlot.IsEmpty &&
                targetInventory is IOccupiedSlotDropHandler occupiedHandler &&
                occupiedHandler.CheckOccupiedSlotDrop(entry, request.TargetBaseSlot))
            {
                return TryExecuteOccupiedHandler(
                    request,
                    sourceInventory,
                    targetInventory,
                    occupiedHandler);
            }

            // Single-entry swap path bypasses the candidate-loop machinery after common rules.
            if (request.Policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap &&
                request.TargetBaseSlot != null &&
                !request.TargetBaseSlot.IsEmpty)
                return TryExecuteSwap(request);

            var strategy = (targetInventory as IPlacementInventory)?.PlacementStrategy;
            if (strategy == null)
                return EntryTransferResult.Failed(requestedAmount, "Target inventory has no strategy");

            if (sourceInventory is not IInventorySnapshotProvider sourceSnapshotProvider ||
                targetInventory is not IInventorySnapshotProvider targetSnapshotProvider)
                return EntryTransferResult.Failed(requestedAmount, "Entry transfer requires snapshot-capable inventories");

            if (!TryResolvePreviewAdapter(sourceInventory, targetInventory, entry.Stack.PrimaryAdapter, out var previewAdapter))
                return EntryTransferResult.Failed(requestedAmount, "Item conversion failed");

            var transaction = new EntryTransaction
            {
                SourceInventory = sourceInventory,
                TargetInventory = targetInventory,
                SourceBaseSlot = entry.SourceBaseSlot,
                SourceSnapshotProvider = sourceSnapshotProvider,
                TargetSnapshotProvider = targetSnapshotProvider,
                SourceSnapshot = sourceSnapshotProvider.CaptureSnapshot(),
                TargetSnapshot = ReferenceEquals(sourceInventory, targetInventory)
                    ? null
                    : targetSnapshotProvider.CaptureSnapshot(),
                SourcePlacementSnapshot = ResolvePlacementSnapshot(sourceInventory, entry.SourceBaseSlot),
                PreviewTargetItemAdapter = previewAdapter,
                RequestedAmount = requestedAmount,
                Remaining = requestedAmount
            };

            var geometry = new InventoryPlacementGeometry(targetInventory);
            var orderer = request.OrdererOverride ?? strategy.DefaultOrderer ?? NaturalPlacementCandidateOrderer.Instance;

            if (request.TargetBaseSlot != null)
            {
                var explicitRequest = CreateAcceptanceRequest(request, transaction);
                bool explicitPlaced =
                    strategy.TryGetCandidate(geometry, explicitRequest, request.TargetBaseSlot, out var explicitCandidate) &&
                    TryApplyCandidate(request, transaction, geometry, explicitCandidate);

                if (transaction.Aborted)
                    return EntryTransferResult.Failed(requestedAmount, "Entry rolled back: source restore failed");

                if (!explicitPlaced)
                {
                    switch (request.Policy.BlockedTargetResolution)
                    {
                        case BlockedTargetResolutionKind.Reject:
                            return EntryTransferResult.Failed(requestedAmount, "Target slot is blocked");
                        case BlockedTargetResolutionKind.Swap:
                            // Occupied explicit target + Swap policy reached here means the target
                            // had no stackable capacity (otherwise explicitPlaced would be true).
                            // Route to the swap path directly.
                            return TryExecuteSwap(request);
                        case BlockedTargetResolutionKind.AlternativeSlots:
                            if (ReferenceEquals(sourceInventory, targetInventory) &&
                                !request.Policy.AllowSameInventoryAlternativePlacement)
                                return EntryTransferResult.Failed(requestedAmount, "Same-inventory alternative placement is not allowed");
                            break;
                    }
                }

                // Both blocked-target alternatives and remainder distribution use the configured
                // alternative orderer; the explicit attempt itself never goes through an orderer.
                orderer = request.OrdererOverride
                    ?? request.Policy.AlternativeOrderer
                    ?? MergeFirstPlacementCandidateOrderer.Instance;
            }

            while (transaction.Remaining > 0 && !transaction.Aborted)
            {
                var acceptanceRequest = CreateAcceptanceRequest(request, transaction);
                var source = strategy.GetCandidates(geometry, acceptanceRequest);
                bool progress = false;

                foreach (var candidate in orderer.Order(source, acceptanceRequest))
                {
                    if (ShouldSkipCandidate(candidate, request, transaction, geometry))
                        continue;

                    if (TryApplyCandidate(request, transaction, geometry, candidate))
                    {
                        progress = true;
                        break;
                    }
                }

                if (!progress)
                    break;
            }

            if (transaction.Aborted)
                return EntryTransferResult.Failed(requestedAmount, "Entry rolled back: source restore failed");

            if (transaction.Committed.Count == 0)
                return EntryTransferResult.Failed(requestedAmount, "No placement accepted the entry");

            if (transaction.Remaining > 0 &&
                request.Policy.PartialTransferMode == PartialTransferMode.RequireFull)
                return RollbackEntry(transaction, "Partial transfer is not allowed");

            return CommitEntry(transaction);
        }

        private static EntryTransferResult TryExecuteOccupiedHandler(
            TransferEntryRequest request,
            IInventory sourceInventory,
            IInventory targetInventory,
            IOccupiedSlotDropHandler handler)
        {
            int requestedAmount = request.Entry.Stack.Count;
            if (sourceInventory is not IInventorySnapshotProvider sourceProvider ||
                targetInventory is not IInventorySnapshotProvider targetProvider)
            {
                return EntryTransferResult.Failed(
                    requestedAmount,
                    "Occupied-slot handler requires snapshot-capable inventories");
            }

            var sourceSnapshot = sourceProvider.CaptureSnapshot();
            var targetSnapshot = ReferenceEquals(sourceInventory, targetInventory)
                ? null
                : targetProvider.CaptureSnapshot();
            var sourceRemovedStack = request.Entry.Stack.CreateCopy();
            var sourcePlacementSnapshot = ResolvePlacementSnapshot(
                sourceInventory,
                request.Entry.SourceBaseSlot);
            var targetPlacementSnapshot = ResolvePlacementSnapshot(
                targetInventory,
                request.TargetBaseSlot);

            try
            {
                if (!handler.ExecuteOccupiedSlotDrop(request.Entry, request.TargetBaseSlot))
                {
                    RestoreOccupiedHandlerSnapshots(
                        sourceInventory, sourceProvider, sourceSnapshot,
                        targetInventory, targetProvider, targetSnapshot);
                    return EntryTransferResult.Failed(requestedAmount, "Occupied-slot handler failed");
                }
            }
            catch (Exception ex)
            {
                RestoreOccupiedHandlerSnapshots(
                    sourceInventory, sourceProvider, sourceSnapshot,
                    targetInventory, targetProvider, targetSnapshot);
                return EntryTransferResult.Failed(requestedAmount, ex.Message);
            }

            sourceInventory.UpdateAllVisuals();
            if (!ReferenceEquals(sourceInventory, targetInventory))
                targetInventory.UpdateAllVisuals();

            var outcome = new PlacementTransferOutcome(
                PlacementTransferOutcomeKind.OccupiedHandler,
                sourceInventory,
                targetInventory,
                request.Entry.SourceBaseSlot,
                request.TargetBaseSlot,
                sourceRemovedStack,
                sourceRemovedStack.CreateCopy(),
                sourcePlacementSnapshot,
                targetPlacementSnapshot,
                targetWasEmptyBefore: false);
            return EntryTransferResult.Committed(
                requestedAmount,
                requestedAmount,
                new[] { outcome });
        }

        private static void RestoreOccupiedHandlerSnapshots(
            IInventory sourceInventory,
            IInventorySnapshotProvider sourceProvider,
            InventorySnapshot sourceSnapshot,
            IInventory targetInventory,
            IInventorySnapshotProvider targetProvider,
            InventorySnapshot targetSnapshot)
        {
            sourceProvider.RestoreSnapshot(sourceSnapshot);
            sourceInventory.UpdateAllVisuals();
            if (targetSnapshot != null)
            {
                targetProvider.RestoreSnapshot(targetSnapshot);
                targetInventory.UpdateAllVisuals();
            }
        }

        private static InventoryAcceptanceRequest CreateAcceptanceRequest(
            TransferEntryRequest request,
            EntryTransaction transaction)
        {
            return new InventoryAcceptanceRequest(
                transaction.TargetInventory,
                transaction.PreviewTargetItemAdapter,
                transaction.Remaining,
                request.Context,
                request.Entry);
        }

        private static bool ShouldSkipCandidate(
            PlacementCandidate candidate,
            TransferEntryRequest request,
            EntryTransaction transaction,
            InventoryPlacementGeometry geometry)
        {
            if (!ReferenceEquals(transaction.SourceInventory, transaction.TargetInventory))
                return false;

            // Same-inventory: while the entry may leave a remainder in the source, the source
            // placement must stay untouchable so the remainder always has a home (plan §5.5).
            if (ReferenceEquals(candidate.Anchor, transaction.SourceBaseSlot))
                return true;

            var sourcePlacement = request.Entry.SourcePlacement;
            if (sourcePlacement == null)
                return false;

            if (ReferenceEquals(candidate.TargetPlacement, sourcePlacement))
                return true;

            return candidate.Anchor != null &&
                   ReferenceEquals(geometry.GetPlacementAt(candidate.Anchor), sourcePlacement);
        }

        private static bool ShouldSkipProbeCandidate(
            PlacementCandidate candidate,
            DragEntry entry,
            IInventory sourceInventory,
            IInventory targetInventory,
            InventoryPlacementGeometry geometry)
        {
            if (!ReferenceEquals(sourceInventory, targetInventory))
                return false;

            if (ReferenceEquals(candidate.Anchor, entry.SourceBaseSlot) ||
                ReferenceEquals(candidate.TargetPlacement, entry.SourcePlacement))
                return true;

            return candidate.Anchor != null &&
                   ReferenceEquals(geometry.GetPlacementAt(candidate.Anchor), entry.SourcePlacement);
        }

        private bool TryApplyCandidate(
            TransferEntryRequest request,
            EntryTransaction transaction,
            InventoryPlacementGeometry geometry,
            PlacementCandidate candidate)
        {
            int amount = Math.Min(transaction.Remaining, candidate.Capacity);
            if (amount <= 0)
                return false;

            var sourceInventory = transaction.SourceInventory;
            var targetInventory = transaction.TargetInventory;
            var sourceSlot = transaction.SourceBaseSlot;
            var placementInventory = targetInventory as IPlacementInventory;
            var sourceCheckpoint = transaction.SourceSnapshotProvider.CaptureSnapshot();
            var targetCheckpoint = ReferenceEquals(sourceInventory, targetInventory)
                ? null
                : transaction.TargetSnapshotProvider.CaptureSnapshot();

            BaseSlot anchorSlot = null;
            BaseSlot createdDynamicSlot = null;
            switch (candidate.Kind)
            {
                case PlacementCandidateKind.Merge:
                    anchorSlot = candidate.TargetPlacement != null
                        ? placementInventory?.GetSlot(candidate.TargetPlacement.AnchorIndex)
                        : candidate.Anchor;
                    break;
                case PlacementCandidateKind.Create:
                    anchorSlot = candidate.Anchor;
                    break;
                case PlacementCandidateKind.NewDynamicSlot:
                    if (targetInventory is not IDynamicSlotLifecycle lifecycle ||
                        !lifecycle.TryCreateSlot(out createdDynamicSlot) ||
                        createdDynamicSlot == null)
                        return false;
                    anchorSlot = createdDynamicSlot;
                    break;
            }

            if (anchorSlot == null)
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            bool targetWasEmpty = anchorSlot.IsEmpty;

            var domainContext = new TransferDomainContext(
                sourceInventory,
                targetInventory,
                sourceSlot,
                anchorSlot,
                request.Entry.Stack.PrimaryAdapter,
                transaction.PreviewTargetItemAdapter,
                amount,
                DetermineTransferKind(candidate, sourceSlot, amount));

            if (!ValidateDomainHandlers(domainContext))
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            // Split exactly the candidate amount straight from the source slot: the remainder never
            // leaves the source, so a partial entry cannot orphan items and the source footprint
            // stays occupied for as long as anything remains in it (plan §5.3/§5.5).
            if (!sourceInventory.TrySplitFromSlot(sourceSlot, amount, out var subStack) ||
                subStack == null || subStack.Count != amount)
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            var sourceRemovedStack = subStack.CreateCopy();

            if (!TransferItemConversionUtility.TryConvertOutgoingStack(sourceInventory, subStack) ||
                !TransferItemConversionUtility.TryConvertIncomingStack(targetInventory, subStack))
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            var transferredStack = subStack.CreateCopy();

            if (!TryMutateTarget(candidate, targetInventory, placementInventory, geometry, anchorSlot, subStack))
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            sourceSlot.UpdateVisuals();
            anchorSlot.UpdateVisuals();
            if (placementInventory?.Grid != null)
                targetInventory.UpdateAllVisuals();
            if (sourceInventory is IPlacementInventory sourcePlacementInventory && sourcePlacementInventory.Grid.HasValue)
                sourceInventory.UpdateAllVisuals();

            domainContext.MarkCommitted(anchorSlot, transferredStack.PrimaryAdapter, amount);

            var outcome = new PlacementTransferOutcome(
                candidate.Kind == PlacementCandidateKind.Merge
                    ? PlacementTransferOutcomeKind.Merge
                    : PlacementTransferOutcomeKind.Create,
                sourceInventory,
                targetInventory,
                sourceSlot,
                anchorSlot,
                sourceRemovedStack,
                transferredStack,
                transaction.SourcePlacementSnapshot,
                ResolvePlacementSnapshot(targetInventory, anchorSlot),
                targetWasEmpty);

            transaction.Committed.Add(new CommittedOutcome { Outcome = outcome, DomainContext = domainContext });
            transaction.Remaining -= amount;
            return true;
        }

        private static bool TryMutateTarget(
            PlacementCandidate candidate,
            IInventory targetInventory,
            IPlacementInventory placementInventory,
            InventoryPlacementGeometry geometry,
            BaseSlot anchorSlot,
            ItemStack subStack)
        {
            if (candidate.Kind == PlacementCandidateKind.Merge)
                return targetInventory.TryAddToSlotStack(anchorSlot, subStack);

            // Create / NewDynamicSlot: defend against custom strategies by re-validating the
            // footprint against the real topology right before mutation.
            var shape = candidate.Shape ?? PlacementShapeUtility.Resolve(subStack.PrimaryAdapter);
            if (placementInventory != null)
            {
                var placementRequest = new PlacementRequest(subStack, anchorSlot.Index, candidate.Orientation, shape);
                return placementInventory.CanPlace(placementRequest) &&
                       placementInventory.TryPlace(placementRequest);
            }

            return anchorSlot.IsEmpty && targetInventory.TrySetStackForSlot(anchorSlot, subStack);
        }

        private static void RestoreCandidateCheckpoint(
            EntryTransaction transaction,
            InventorySnapshot sourceCheckpoint,
            InventorySnapshot targetCheckpoint)
        {
            try
            {
                transaction.SourceSnapshotProvider.RestoreSnapshot(sourceCheckpoint);
                transaction.SourceInventory.UpdateAllVisuals();
                if (targetCheckpoint != null)
                {
                    transaction.TargetSnapshotProvider.RestoreSnapshot(targetCheckpoint);
                    transaction.TargetInventory.UpdateAllVisuals();
                }
            }
            catch (Exception ex)
            {
                Extensions.DragAndDropLog($"<color=red>[InventoryTransferService] Candidate rollback failed: {ex.Message}</color>");
                RestoreSnapshots(transaction);
                transaction.Committed.Clear();
                transaction.Remaining = transaction.RequestedAmount;
                transaction.Aborted = true;
            }
        }

        private static EntryTransferResult RollbackEntry(EntryTransaction transaction, string reason)
        {
            RestoreSnapshots(transaction);
            return EntryTransferResult.Failed(transaction.RequestedAmount, reason);
        }

        private static void RestoreSnapshots(EntryTransaction transaction)
        {
            transaction.SourceSnapshotProvider.RestoreSnapshot(transaction.SourceSnapshot);
            transaction.SourceInventory.UpdateAllVisuals();
            if (transaction.TargetSnapshot != null)
            {
                transaction.TargetSnapshotProvider.RestoreSnapshot(transaction.TargetSnapshot);
                transaction.TargetInventory.UpdateAllVisuals();
            }
        }

        private static EntryTransferResult CommitEntry(EntryTransaction transaction)
        {
            var outcomes = new List<PlacementTransferOutcome>(transaction.Committed.Count);
            int transferred = 0;

            foreach (var committed in transaction.Committed)
            {
                outcomes.Add(committed.Outcome);
                transferred += committed.Outcome.Amount;

                foreach (var handler in EnumerateDomainHandlers(committed.DomainContext))
                {
                    try
                    {
                        handler.OnTransferSucceeded(committed.DomainContext);
                    }
                    catch (Exception ex)
                    {
                        Extensions.DragAndDropLog($"<color=red>[InventoryTransferService] Domain success hook threw: {ex.Message}</color>");
                    }
                }

                DispatchOutcomeEvents(committed.Outcome);
            }

            if (transaction.SourceBaseSlot.IsEmpty &&
                transaction.SourceInventory is IDynamicSlotLifecycle sourceLifecycle)
                sourceLifecycle.HandleSlotEmptied(transaction.SourceBaseSlot);

            return EntryTransferResult.Committed(transaction.RequestedAmount, transferred, outcomes);
        }

        private static void DispatchOutcomeEvents(PlacementTransferOutcome outcome)
        {
            if (outcome.SourceItem == null || outcome.TargetItem == null || outcome.Amount <= 0)
                return;

            if (outcome.SourceInventory is IInventoryEventSink sourceEventSink)
            {
                sourceEventSink.EmitItemRemoved(
                    outcome.SourceRemovedStack,
                    outcome.SourceBaseSlot?.Index ?? -1,
                    outcome.TargetInventory,
                    outcome.SourceBaseSlot,
                    outcome.TargetBaseSlot,
                    outcome.SourcePlacementSnapshot);
            }

            if (outcome.TargetInventory is IInventoryEventSink targetEventSink && outcome.TargetBaseSlot != null)
            {
                targetEventSink.EmitItemAdded(
                    outcome.TransferredStack,
                    outcome.TargetBaseSlot.Index,
                    outcome.SourceInventory,
                    outcome.SourceBaseSlot,
                    outcome.TargetBaseSlot,
                    outcome.TargetPlacementSnapshot);
            }
        }

        private static bool ValidateDomainHandlers(TransferDomainContext context)
        {
            foreach (var handler in EnumerateDomainHandlers(context))
            {
                try
                {
                    var result = handler.CanCommitTransfer(context);
                    if (!result.IsValid)
                        return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateTransferStart(
            DragContext context,
            IInventory targetInventory,
            out string failureReason)
        {
            failureReason = null;
            var handlers = new HashSet<ITransferDomainHandler>();

            foreach (var entry in context.Entries)
            {
                var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                if (sourceInventory?.DataBinding is ITransferDomainHandler sourceHandler)
                    handlers.Add(sourceHandler);
            }

            if (targetInventory?.DataBinding is ITransferDomainHandler targetHandler)
                handlers.Add(targetHandler);

            foreach (var handler in handlers)
            {
                try
                {
                    var result = handler.CanStartTransfer(context, targetInventory);
                    if (result.IsValid)
                        continue;

                    failureReason = string.IsNullOrEmpty(result.FailureReason)
                        ? "Transfer rejected by domain handler"
                        : result.FailureReason;
                    return false;
                }
                catch (Exception ex)
                {
                    failureReason = ex.Message;
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<ITransferDomainHandler> EnumerateDomainHandlers(TransferDomainContext context)
        {
            var sourceHandler = context.SourceInventory?.DataBinding as ITransferDomainHandler;
            if (sourceHandler != null)
                yield return sourceHandler;

            var targetHandler = context.TargetInventory?.DataBinding as ITransferDomainHandler;
            if (targetHandler != null && targetHandler != sourceHandler)
                yield return targetHandler;
        }

        private static TransferKind DetermineTransferKind(PlacementCandidate candidate, BaseSlot sourceSlot, int amount)
        {
            if (candidate.Kind == PlacementCandidateKind.Merge)
                return TransferKind.Merge;

            return sourceSlot?.Stack != null && sourceSlot.Stack.Count > amount
                ? TransferKind.Split
                : TransferKind.Move;
        }

        private static bool TryResolvePreviewAdapter(
            IInventory sourceInventory,
            IInventory targetInventory,
            IItemAdapter sourceAdapter,
            out IItemAdapter previewAdapter)
        {
            previewAdapter = null;
            if (sourceAdapter == null)
                return false;

            if (!ItemStack.TryCreate(new[] { sourceAdapter }, out var previewStack))
                return false;

            if (!TransferItemConversionUtility.TryConvertOutgoingStack(sourceInventory, previewStack) ||
                !TransferItemConversionUtility.TryConvertIncomingStack(targetInventory, previewStack))
                return false;

            previewAdapter = previewStack.PrimaryAdapter;
            return previewAdapter != null;
        }

        // ──── Swap ─────────────────────────────────────────────────────────────────────────────

        private EntryTransferResult TryExecuteSwap(TransferEntryRequest request)
        {
            var entry = request.Entry;
            var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
            var targetInventory = request.TargetInventory;
            var sourceSlot = entry.SourceBaseSlot;
            var targetSlot = request.TargetBaseSlot;
            int requestedAmount = entry.Stack.Count;

            if (sourceInventory == null || targetInventory == null || sourceSlot == null || targetSlot == null)
                return EntryTransferResult.Failed(requestedAmount, "Swap: invalid source/target");

            if (sourceSlot.IsEmpty || targetSlot.IsEmpty)
                return EntryTransferResult.Failed(requestedAmount, "Swap requires non-empty source and target");

            // Swap transfers the entire source placement stack.
            if (sourceSlot.Stack?.Count != requestedAmount)
                return EntryTransferResult.Failed(requestedAmount, "Swap requires the entire source stack");

            if (sourceInventory is not IInventorySnapshotProvider sourceSnapshotProvider ||
                targetInventory is not IInventorySnapshotProvider targetSnapshotProvider)
                return EntryTransferResult.Failed(requestedAmount, "Swap requires snapshot-capable inventories");

            if (sourceInventory is not IPlacementInventory sourcePlacementInventory ||
                targetInventory is not IPlacementInventory targetPlacementInventory)
                return EntryTransferResult.Failed(requestedAmount, "Swap requires placement-capable inventories");

            var sourcePlacement = sourcePlacementInventory.GetPlacementAt(sourceSlot);
            var targetPlacement = targetPlacementInventory.GetPlacementAt(targetSlot);
            if (sourcePlacement?.Stack == null || targetPlacement?.Stack == null)
                return EntryTransferResult.Failed(requestedAmount, "Swap: cannot resolve placements");

            // Clone stacks for events (before mutation) and build converted copies for placement.
            var sourceStackBefore = sourcePlacement.Stack.CreateCopy();
            var targetStackBefore = targetPlacement.Stack.CreateCopy();

            // Forward: source item will be placed in the target inventory.
            if (!ItemStack.TryCreate(sourcePlacement.Stack.Adapters, out var targetStackAfter) ||
                !TransferItemConversionUtility.TryConvertOutgoingStack(sourceInventory, targetStackAfter) ||
                !TransferItemConversionUtility.TryConvertIncomingStack(targetInventory, targetStackAfter))
                return EntryTransferResult.Failed(requestedAmount, "Swap: forward conversion failed");

            // Reverse: target item will be placed in the source inventory.
            if (!ItemStack.TryCreate(targetPlacement.Stack.Adapters, out var sourceStackAfter) ||
                !TransferItemConversionUtility.TryConvertOutgoingStack(targetInventory, sourceStackAfter) ||
                !TransferItemConversionUtility.TryConvertIncomingStack(sourceInventory, sourceStackAfter))
                return EntryTransferResult.Failed(requestedAmount, "Swap: reverse conversion failed");

            // Domain validation in both directions.
            var forwardDomain = new TransferDomainContext(
                sourceInventory, targetInventory, sourceSlot, targetSlot,
                sourceStackBefore.PrimaryAdapter, targetStackAfter.PrimaryAdapter,
                sourceStackBefore.Count, TransferKind.Swap);
            var reverseDomain = new TransferDomainContext(
                targetInventory, sourceInventory, targetSlot, sourceSlot,
                targetStackBefore.PrimaryAdapter, sourceStackAfter.PrimaryAdapter,
                targetStackBefore.Count, TransferKind.Swap);
            forwardDomain.CounterpartContext = reverseDomain;
            reverseDomain.CounterpartContext = forwardDomain;

            if (!ValidateDomainHandlers(forwardDomain) || !ValidateDomainHandlers(reverseDomain))
                return EntryTransferResult.Failed(requestedAmount, "Swap: domain validation failed");

            // Optional UI callback before mutation.
            var swapContext = new InventorySwapContext(
                sourceStackBefore, targetStackBefore, sourceSlot, targetSlot,
                sourceInventory, targetInventory);
            if (request.SwapAttempting != null && !request.SwapAttempting(swapContext))
                return EntryTransferResult.Failed(requestedAmount, "Swap cancelled by listener");

            var sourceSnapshot = sourceSnapshotProvider.CaptureSnapshot();
            var targetSnapshot = ReferenceEquals(sourceInventory, targetInventory)
                ? null
                : targetSnapshotProvider.CaptureSnapshot();

            var sourceRemovedSnapshot = PlacementSnapshot.FromPlacement(sourcePlacement, sourcePlacementInventory.GetSlot);
            var targetRemovedSnapshot = PlacementSnapshot.FromPlacement(targetPlacement, targetPlacementInventory.GetSlot);

            if (!sourcePlacementInventory.RemovePlacement(sourcePlacement) ||
                !targetPlacementInventory.RemovePlacement(targetPlacement))
            {
                RestoreSwapSnapshots(sourceInventory, sourceSnapshotProvider, sourceSnapshot,
                    targetInventory, targetSnapshotProvider, targetSnapshot);
                return EntryTransferResult.Failed(requestedAmount, "Swap: failed to vacate placements");
            }

            // Each item keeps its own footprint while moving to the opposite anchor.
            var forwardShape = PlacementShapeUtility.Resolve(targetStackAfter.PrimaryAdapter)
                ?? sourcePlacement.Shape;
            var forwardReq = new PlacementRequest(
                targetStackAfter.CreateCopy(), targetPlacement.AnchorIndex,
                sourcePlacement.Orientation, forwardShape);
            if (!targetPlacementInventory.TryPlace(forwardReq, out var forwardPlacement))
            {
                RestoreSwapSnapshots(sourceInventory, sourceSnapshotProvider, sourceSnapshot,
                    targetInventory, targetSnapshotProvider, targetSnapshot);
                return EntryTransferResult.Failed(requestedAmount, "Swap: cannot place source item in target");
            }

            var reverseShape = PlacementShapeUtility.Resolve(sourceStackAfter.PrimaryAdapter)
                ?? targetPlacement.Shape;
            var reverseReq = new PlacementRequest(
                sourceStackAfter.CreateCopy(), sourcePlacement.AnchorIndex,
                targetPlacement.Orientation, reverseShape);
            if (!sourcePlacementInventory.TryPlace(reverseReq, out var reversePlacement))
            {
                RestoreSwapSnapshots(sourceInventory, sourceSnapshotProvider, sourceSnapshot,
                    targetInventory, targetSnapshotProvider, targetSnapshot);
                return EntryTransferResult.Failed(requestedAmount, "Swap: cannot place target item in source");
            }

            sourceInventory.UpdateAllVisuals();
            targetInventory.UpdateAllVisuals();

            var forwardAddedSnapshot = PlacementSnapshot.FromPlacement(forwardPlacement, targetPlacementInventory.GetSlot);
            var reverseAddedSnapshot = PlacementSnapshot.FromPlacement(reversePlacement, sourcePlacementInventory.GetSlot);

            // Commit domain hooks.
            forwardDomain.MarkCommitted(targetSlot, targetStackAfter.PrimaryAdapter, sourceStackBefore.Count);
            reverseDomain.MarkCommitted(sourceSlot, sourceStackAfter.PrimaryAdapter, targetStackBefore.Count);
            foreach (var h in EnumerateDomainHandlers(forwardDomain))
                try { h.OnTransferSucceeded(forwardDomain); } catch (Exception ex)
                { Extensions.DragAndDropLog($"<color=red>[InventoryTransferService] Swap domain hook threw: {ex.Message}</color>"); }
            foreach (var h in EnumerateDomainHandlers(reverseDomain))
                try { h.OnTransferSucceeded(reverseDomain); } catch (Exception ex)
                { Extensions.DragAndDropLog($"<color=red>[InventoryTransferService] Swap domain hook threw: {ex.Message}</color>"); }

            DispatchSwapEvents(
                sourceInventory, targetInventory, sourceSlot, targetSlot,
                sourceStackBefore, targetStackBefore, targetStackAfter, sourceStackAfter,
                sourceRemovedSnapshot, targetRemovedSnapshot,
                forwardAddedSnapshot, reverseAddedSnapshot);

            request.SwapCompleted?.Invoke(swapContext);

            var outcome = new PlacementTransferOutcome(
                PlacementTransferOutcomeKind.Swap,
                sourceInventory, targetInventory,
                sourceSlot, targetSlot,
                sourceStackBefore, targetStackAfter,
                sourceRemovedSnapshot, forwardAddedSnapshot,
                targetWasEmptyBefore: false);

            return EntryTransferResult.Committed(requestedAmount, requestedAmount, new[] { outcome });
        }

        private static void RestoreSwapSnapshots(
            IInventory sourceInventory,
            IInventorySnapshotProvider sourceProvider,
            InventorySnapshot sourceSnapshot,
            IInventory targetInventory,
            IInventorySnapshotProvider targetProvider,
            InventorySnapshot targetSnapshot)
        {
            sourceProvider.RestoreSnapshot(sourceSnapshot);
            sourceInventory.UpdateAllVisuals();
            if (targetSnapshot != null)
            {
                targetProvider.RestoreSnapshot(targetSnapshot);
                targetInventory.UpdateAllVisuals();
            }
        }

        private static void DispatchSwapEvents(
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot sourceSlot,
            BaseSlot targetSlot,
            ItemStack sourceStackBefore,
            ItemStack targetStackBefore,
            ItemStack targetStackAfter,
            ItemStack sourceStackAfter,
            PlacementSnapshot sourceRemovedSnapshot,
            PlacementSnapshot targetRemovedSnapshot,
            PlacementSnapshot forwardAddedSnapshot,
            PlacementSnapshot reverseAddedSnapshot)
        {
            if (targetInventory is IInventoryEventSink targetEventSink)
            {
                targetEventSink.EmitItemRemoved(
                    targetStackBefore, targetSlot.Index, sourceInventory,
                    targetSlot, sourceSlot, targetRemovedSnapshot);
                targetEventSink.EmitItemAdded(
                    targetStackAfter, targetSlot.Index, sourceInventory,
                    sourceSlot, targetSlot, forwardAddedSnapshot);
            }

            if (sourceInventory is IInventoryEventSink sourceEventSink)
            {
                sourceEventSink.EmitItemRemoved(
                    sourceStackBefore, sourceSlot.Index, targetInventory,
                    sourceSlot, targetSlot, sourceRemovedSnapshot);
                sourceEventSink.EmitItemAdded(
                    sourceStackAfter, sourceSlot.Index, targetInventory,
                    targetSlot, sourceSlot, reverseAddedSnapshot);
            }
        }

        private static PlacementSnapshot ResolvePlacementSnapshot(IInventory inventory, BaseSlot slot)
        {
            if (slot == null)
                return null;

            if (inventory is IPlacementInventory placementInventory)
            {
                var placement = placementInventory.GetPlacementAt(slot);
                if (placement != null)
                    return PlacementSnapshot.FromPlacement(placement, placementInventory.GetSlot);
            }

            return new PlacementSnapshot(
                slot.Index,
                PlacementOrientation.Rot0,
                Vector2Int.one,
                new[] { slot.Index },
                slot,
                new[] { slot },
                coveredOffsets: new[] { Vector2Int.zero });
        }
    }
}
