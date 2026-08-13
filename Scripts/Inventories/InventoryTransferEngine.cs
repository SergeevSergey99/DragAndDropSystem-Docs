using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UDND.Core;
using UDND.Rules;
using UDND.Slots;
using UDND.Tools;

namespace UDND.Inventories
{
    /// <summary>
    /// Input for the low-level single-entry JIT operation.
    /// Transfer-wide validation belongs to ExecuteBatch/ExecuteBatchAsync.
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
    /// directly through narrow placement primitives. Regular entries are not preplanned; atomic
    /// multi-swap is the bounded exception and resolves only its displacement set before mutation.
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
            public TransferConversionSession ConversionSession;
            public int RequestedAmount;
            public int Remaining;
            public bool Aborted;
            public List<CommittedOutcome> Committed = new List<CommittedOutcome>();
        }

        private sealed class ResolvedSwapDisplacement
        {
            public Placement Placement;
            public BaseSlot SourceSlot;
            public BaseSlot DestinationSlot;
            public ItemStack StackBefore;
            public ItemStack ConvertedStack;
            public IPlacementShape ConvertedShape;
            public int ConvertedOrientation;
            public TransferDomainContext DomainContext;
        }

        private sealed class ResolvedSwap
        {
            public IInventory SourceInventory;
            public IInventory TargetInventory;
            public IPlacementInventory SourcePlacementInventory;
            public IPlacementInventory TargetPlacementInventory;
            public IInventorySnapshotProvider SourceSnapshotProvider;
            public IInventorySnapshotProvider TargetSnapshotProvider;
            public BaseSlot SourceSlot;
            public BaseSlot TargetSlot;
            public BaseSlot ForwardAnchor;
            public Placement SourcePlacement;
            public Placement PrimaryTargetPlacement;
            public ItemStack SourceStackBefore;
            public ItemStack ForwardStack;
            public IPlacementShape ForwardShape;
            public int ForwardOrientation;
            public IReadOnlyList<BaseSlot> ForwardCoveredSlots;
            public TransferDomainContext ForwardDomain;
            public readonly List<ResolvedSwapDisplacement> Displacements =
                new List<ResolvedSwapDisplacement>();

            // Search state, only filled for PartialOverlapSwapMode.VacatedArea.
            public HashSet<int> VacatedCells;
            public HashSet<Placement> RemovedPlacements;
            public HashSet<int> IncomingCells;
            public readonly HashSet<int> ClaimedCells = new HashSet<int>();
        }

        public TransferProbe Probe(
            DragContext context,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ResolvedDropPolicy policy,
            GlobalRuleValidator globalRules = null)
        {
            if (context?.Entries == null || context.Entries.Count == 0 || targetInventory == null)
                return TransferProbe.Rejected("Empty drag context or target inventory");

            if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap &&
                context.Entries.Count > 1)
                return TransferProbe.Rejected("Swap requires a single full entry");

            var validationContext = context.WithTarget(targetBaseSlot, targetInventory);
            if (!ValidateTransferStart(validationContext, targetInventory, out var startFailure))
                return TransferProbe.Rejected(startFailure);

            var strategy = (targetInventory as IPlacementInventory)?.Strategy;
            if (strategy == null)
                return TransferProbe.Rejected("Target inventory has no strategy");

            var geometry = new InventoryPlacementGeometry(targetInventory);
            string failureReason = "No placement accepted the transfer";
            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                var entryTargetSlot = i == 0 ? targetBaseSlot : null;
                var entryContext = context.WithTarget(entryTargetSlot, targetInventory);

                // An explicit target slot must belong to the target inventory; a foreign slot is a
                // blocked explicit target, handled by the blocked-target policy below (mirrors
                // TryTransferEntry so CanAcceptDrop and ProcessDrop stay consistent).
                bool entryTargetOwned = entryTargetSlot != null &&
                    ReferenceEquals(entryTargetSlot.Inventory, targetInventory);

                // Pre-rule occupied-slot handler: a different destination (e.g. a container),
                // so it is consulted before and bypasses the target drop/placement rules.
                if (entryTargetOwned && !entryTargetSlot.IsEmpty &&
                    ResolvePreRuleOccupiedHandler(targetInventory) is { } preRuleHandler &&
                    preRuleHandler.CheckOccupiedSlotDrop(entry, entryTargetSlot))
                {
                    return TransferProbe.Accepted(
                        i,
                        entry,
                        anchorSlot: entryTargetSlot,
                        coveredSlots: new[] { entryTargetSlot },
                        isExplicitTargetCandidate: true);
                }

                var rules = new RuleEvaluationService().ValidateEntryDrop(entryContext, entry, globalRules);
                if (!rules.IsValid || sourceInventory == null || entry.Stack?.PrimaryAdapter == null)
                {
                    if (!rules.IsValid && !string.IsNullOrEmpty(rules.FailureReason))
                        failureReason = rules.FailureReason;
                    continue;
                }

                // Post-rule occupied-slot handler: runs only after the target drop rules pass.
                if (entryTargetOwned && !entryTargetSlot.IsEmpty &&
                    ResolvePostRuleOccupiedHandler(targetInventory) is { } postRuleHandler &&
                    postRuleHandler.CheckOccupiedSlotDrop(entry, entryTargetSlot))
                {
                    return TransferProbe.Accepted(
                        i,
                        entry,
                        anchorSlot: entryTargetSlot,
                        coveredSlots: new[] { entryTargetSlot },
                        isExplicitTargetCandidate: true);
                }

                if (!TryResolvePreviewAdapter(
                        sourceInventory,
                        targetInventory,
                        entry.Stack.PrimaryAdapter,
                        context.ConversionSession,
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
                    if (entryTargetOwned && strategy.TryGetCandidate(
                            geometry,
                            acceptance,
                            entryTargetSlot,
                            out var explicitCandidate))
                    {
                        return CreateAcceptedProbe(
                            i,
                            entry,
                            explicitCandidate,
                            geometry,
                            isExplicitTargetCandidate: true);
                    }

                    if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Reject)
                    {
                        failureReason = "Target slot is blocked";
                        continue;
                    }

                    if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap)
                    {
                        // No IsEmpty gate here: whether this is a swap depends on what the incoming
                        // footprint covers, not on what sits under the pointer cell. The resolver
                        // reports "does not displace another placement" when nothing is covered.
                        if (entryTargetOwned)
                        {
                            var swapRequest = new TransferEntryRequest(
                                context,
                                entry,
                                targetInventory,
                                entryTargetSlot,
                                policy,
                                globalRules: globalRules);
                            if (!TryResolveSwap(swapRequest, out var resolvedSwap, out var swapFailure))
                            {
                                failureReason = swapFailure;
                                continue;
                            }

                            return TransferProbe.Accepted(
                                i,
                                entry,
                                anchorSlot: resolvedSwap.ForwardAnchor,
                                coveredSlots: resolvedSwap.ForwardCoveredSlots,
                                isExplicitTargetCandidate: true);
                        }

                        failureReason = "Swap target is not owned by the target inventory";
                        continue;
                    }

                    if (ReferenceEquals(sourceInventory, targetInventory) &&
                        !policy.AllowSameInventoryAlternativePlacement)
                    {
                        failureReason = "Same-inventory alternative placement is not allowed";
                        continue;
                    }
                }

                var orderer = entryTargetSlot != null
                    ? policy.AlternativeOrderer
                    : NaturalPlacementCandidateOrderer.Instance;
                orderer ??= NaturalPlacementCandidateOrderer.Instance;
                foreach (var candidate in orderer.Order(strategy.GetCandidates(geometry, acceptance), acceptance))
                {
                    if (!ShouldSkipProbeCandidate(candidate, entry, sourceInventory, targetInventory, geometry))
                        return CreateAcceptedProbe(i, entry, candidate, geometry);
                }
            }

            return TransferProbe.Rejected(failureReason);
        }

        private static TransferProbe CreateAcceptedProbe(
            int entryIndex,
            DragEntry entry,
            PlacementCandidate candidate,
            InventoryPlacementGeometry geometry,
            bool isExplicitTargetCandidate = false)
        {
            IReadOnlyList<BaseSlot> coveredSlots = Array.Empty<BaseSlot>();
            var anchor = candidate.Anchor;
            if (anchor == null && candidate.TargetPlacement != null)
                anchor = geometry.Inventory.GetSlot(candidate.TargetPlacement.AnchorIndex);

            if (anchor != null)
            {
                coveredSlots = geometry.GetCoveredSlots(
                    anchor,
                    candidate.Shape ?? entry.Shape,
                    candidate.Orientation);
            }

            return TransferProbe.Accepted(
                entryIndex,
                entry,
                candidate,
                anchor,
                coveredSlots,
                isExplicitTargetCandidate);
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
            if (HasAsyncTransferStartHandler(validationContext, targetInventory))
                return TransferExecutionReport.Rejected(
                    "Transfer requires asynchronous execution");
            if (!ValidateTransferStart(validationContext, targetInventory, out var rejectionReason))
                return TransferExecutionReport.Rejected(rejectionReason);

            return ExecuteBatchCore(
                context,
                targetInventory,
                targetBaseSlot,
                policy,
                swapAttempting,
                swapCompleted,
                globalRules);
        }

        public async Task<TransferExecutionReport> ExecuteBatchAsync(
            DragContext context,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ResolvedDropPolicy policy,
            Func<InventorySwapContext, bool> swapAttempting = null,
            Action<InventorySwapContext> swapCompleted = null,
            GlobalRuleValidator globalRules = null,
            CancellationToken cancellationToken = default)
        {
            if (context?.Entries == null || context.Entries.Count == 0)
                return TransferExecutionReport.Rejected("Empty drag context");

            if (policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap &&
                context.Entries.Count > 1)
                return TransferExecutionReport.Rejected("Swap requires a single full entry");

            cancellationToken.ThrowIfCancellationRequested();
            var validationContext = context.WithTarget(targetBaseSlot, targetInventory);
            if (!ValidateTransferStart(validationContext, targetInventory, out var rejectionReason))
                return TransferExecutionReport.Rejected(rejectionReason);

            var asyncValidation = await ValidateTransferStartAsync(
                validationContext,
                targetInventory,
                cancellationToken);
            if (!asyncValidation.IsValid)
                return TransferExecutionReport.Rejected(asyncValidation.FailureReason);

            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteBatchCore(
                context,
                targetInventory,
                targetBaseSlot,
                policy,
                swapAttempting,
                swapCompleted,
                globalRules);
        }

        private TransferExecutionReport ExecuteBatchCore(
            DragContext context,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ResolvedDropPolicy policy,
            Func<InventorySwapContext, bool> swapAttempting,
            Action<InventorySwapContext> swapCompleted,
            GlobalRuleValidator globalRules)
        {
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

        /// <summary>
        /// Executes one already-authorized entry. Callers that need transfer-wide domain validation
        /// must use ExecuteBatch or ExecuteBatchAsync instead.
        /// </summary>
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

            // An explicit target slot is only placeable when it actually belongs to the target
            // inventory. A foreign slot reaches here when an occupied-slot handler routes a
            // same-inventory drop through the container's own slot (which lives in a different
            // inventory): treat it as a blocked explicit target so the blocked-target policy —
            // including AllowSameInventoryAlternativePlacement — decides the outcome, instead of
            // re-triggering the occupied handler or placing onto an unrelated slot.
            bool hasOwnedTargetSlot = request.TargetBaseSlot != null &&
                ReferenceEquals(request.TargetBaseSlot.Inventory, targetInventory);

            // Pre-rule occupied-slot handler: bypasses the target drop rules (different destination).
            if (hasOwnedTargetSlot &&
                !request.TargetBaseSlot.IsEmpty &&
                ResolvePreRuleOccupiedHandler(targetInventory) is { } preRuleHandler &&
                preRuleHandler.CheckOccupiedSlotDrop(entry, request.TargetBaseSlot))
            {
                if (TryExecuteOccupiedHandler(
                        request, sourceInventory, targetInventory, preRuleHandler, out var preRuleResult))
                    return preRuleResult;
                // OccupiedSlotDropResult.Fallthrough: continue with the normal pipeline below.
            }

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

            // Post-rule occupied-slot handler: runs only after the target drop rules pass.
            if (hasOwnedTargetSlot &&
                !request.TargetBaseSlot.IsEmpty &&
                ResolvePostRuleOccupiedHandler(targetInventory) is { } postRuleHandler &&
                postRuleHandler.CheckOccupiedSlotDrop(entry, request.TargetBaseSlot))
            {
                if (TryExecuteOccupiedHandler(
                        request, sourceInventory, targetInventory, postRuleHandler, out var postRuleResult))
                    return postRuleResult;
                // OccupiedSlotDropResult.Fallthrough: continue with the normal pipeline below.
            }

            // Single-entry swap path bypasses the candidate-loop machinery after common rules.
            // A cell "occupied" by the dragged item itself is not a swap target: the source
            // placement is vacated before the new one lands, so a move or rotation over its own
            // footprint is an ordinary placement. Such a drop goes through the candidate path
            // first; if that path finds nothing, the blocked-target switch below still routes it
            // here, so a self-overlapping footprint that also covers other items still swaps.
            if (request.Policy.BlockedTargetResolution == BlockedTargetResolutionKind.Swap &&
                hasOwnedTargetSlot &&
                !request.TargetBaseSlot.IsEmpty &&
                !IsCoveredBySourcePlacement(request, sourceInventory, targetInventory))
                return TryExecuteSwap(request);

            var strategy = (targetInventory as IPlacementInventory)?.Strategy;
            if (strategy == null)
                return EntryTransferResult.Failed(requestedAmount, "Target inventory has no strategy");

            if (sourceInventory is not IInventorySnapshotProvider sourceSnapshotProvider ||
                targetInventory is not IInventorySnapshotProvider targetSnapshotProvider)
                return EntryTransferResult.Failed(requestedAmount, "Entry transfer requires snapshot-capable inventories");

            var conversionSession = request.Context?.ConversionSession;
            if (!TryResolvePreviewAdapter(
                    sourceInventory,
                    targetInventory,
                    entry.Stack.PrimaryAdapter,
                    conversionSession,
                    out var previewAdapter))
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
                ConversionSession = conversionSession,
                RequestedAmount = requestedAmount,
                Remaining = requestedAmount
            };

            var geometry = new InventoryPlacementGeometry(targetInventory);
            var orderer = request.OrdererOverride
                ?? request.Policy.AlternativeOrderer
                ?? NaturalPlacementCandidateOrderer.Instance;

            if (request.TargetBaseSlot != null)
            {
                bool explicitPlaced = false;
                if (hasOwnedTargetSlot)
                {
                    var explicitRequest = CreateAcceptanceRequest(request, transaction);
                    explicitPlaced =
                        strategy.TryGetCandidate(geometry, explicitRequest, request.TargetBaseSlot, out var explicitCandidate) &&
                        TryApplyCandidate(request, transaction, geometry, explicitCandidate);

                    if (transaction.Aborted)
                        return EntryTransferResult.Failed(requestedAmount, "Entry rolled back: source restore failed");
                }

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
                        case BlockedTargetResolutionKind.FindAlternative:
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
                    ?? NaturalPlacementCandidateOrderer.Instance;
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

        /// <summary>
        /// Occupied-slot drop behavior lives on the DataBinding, not the inventory.
        /// The pipeline resolves it through the target's DataBinding so inventories stay
        /// free of the IOccupiedSlotDropHandler contract. The timing variant decides whether
        /// the handler is consulted before or after the target drop rules.
        /// </summary>
        private static IPreRuleOccupiedSlotDropHandler ResolvePreRuleOccupiedHandler(IInventory inventory)
            => inventory?.DataBinding as IPreRuleOccupiedSlotDropHandler;

        private static IPostRuleOccupiedSlotDropHandler ResolvePostRuleOccupiedHandler(IInventory inventory)
            => inventory?.DataBinding as IPostRuleOccupiedSlotDropHandler;

        /// <summary>
        /// Runs the occupied-slot handler and maps its <see cref="OccupiedSlotDropResult"/> to a
        /// pipeline decision. Returns true when the handler consumed the entry (Handled/Rejected,
        /// with <paramref name="result"/> set); returns false when the handler fell through, in
        /// which case the caller must continue the normal transfer pipeline.
        /// </summary>
        private static bool TryExecuteOccupiedHandler(
            TransferEntryRequest request,
            IInventory sourceInventory,
            IInventory targetInventory,
            IOccupiedSlotDropHandler handler,
            out EntryTransferResult result)
        {
            result = null;
            int requestedAmount = request.Entry.Stack.Count;
            if (sourceInventory is not IInventorySnapshotProvider sourceProvider ||
                targetInventory is not IInventorySnapshotProvider targetProvider)
            {
                result = EntryTransferResult.Failed(
                    requestedAmount,
                    "Occupied-slot handler requires snapshot-capable inventories");
                return true;
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

            OccupiedSlotDropResult outcomeKind;
            try
            {
                outcomeKind = handler.ExecuteOccupiedSlotDrop(request.Entry, request.TargetBaseSlot);
            }
            catch (Exception ex)
            {
                RestoreOccupiedHandlerSnapshots(
                    sourceInventory, sourceProvider, sourceSnapshot,
                    targetInventory, targetProvider, targetSnapshot);
                result = EntryTransferResult.Failed(requestedAmount, ex.Message);
                return true;
            }

            switch (outcomeKind)
            {
                case OccupiedSlotDropResult.Fallthrough:
                    // Handler declined: undo anything it touched and let the normal pipeline run.
                    RestoreOccupiedHandlerSnapshots(
                        sourceInventory, sourceProvider, sourceSnapshot,
                        targetInventory, targetProvider, targetSnapshot);
                    return false;

                case OccupiedSlotDropResult.Rejected:
                    RestoreOccupiedHandlerSnapshots(
                        sourceInventory, sourceProvider, sourceSnapshot,
                        targetInventory, targetProvider, targetSnapshot);
                    result = EntryTransferResult.Failed(requestedAmount, "Occupied-slot handler rejected the drop");
                    return true;

                case OccupiedSlotDropResult.Handled:
                default:
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
                    result = EntryTransferResult.Committed(
                        requestedAmount,
                        requestedAmount,
                        new[] { outcome });
                    return true;
            }
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
                    // Resolve the merge target through a cell the placement actually covers, not its
                    // anchor index: a complex shape's anchor (bbox origin) may be an empty notch or a
                    // cell owned by an interlocking neighbor, so GetAt(anchorIndex) would miss the
                    // placement and the merge would write to the wrong stack (or spawn a stray one).
                    anchorSlot = candidate.TargetPlacement != null
                        ? placementInventory?.GetSlot(ResolvePlacementPrimaryIndex(candidate.TargetPlacement))
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

            // Resolved through the drag's conversion session: these are the very objects the probe
            // and the drop preview validated, not fresh copies of them.
            if (!TransferItemConversionUtility.TryConvertStackToTargetDomain(
                    sourceInventory,
                    targetInventory,
                    subStack,
                    transaction.ConversionSession))
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            var transferredStack = subStack.CreateCopy();

            if (!TryMutateTarget(candidate, targetInventory, placementInventory, geometry, anchorSlot, subStack, out var resultPlacement))
            {
                RestoreCandidateCheckpoint(transaction, sourceCheckpoint, targetCheckpoint);
                return false;
            }

            sourceSlot.UpdateVisuals();
            anchorSlot.UpdateVisuals();
            if (placementInventory != null &&
                placementInventory.GetCoveredCells(
                    anchorSlot.Index,
                    candidate.Shape,
                    candidate.Orientation).Count > 1)
                targetInventory.UpdateAllVisuals();
            if (transaction.SourcePlacementSnapshot?.CoveredIndices != null &&
                transaction.SourcePlacementSnapshot.CoveredIndices.Count > 1)
                sourceInventory.UpdateAllVisuals();

            domainContext.MarkCommitted(anchorSlot, transferredStack.PrimaryAdapter, amount);

            // Snapshot the placement we actually created/merged, not a cell lookup at the anchor:
            // a complex shape's anchor cell (bounding-box origin) may be empty or covered by a
            // neighboring interlocking placement, so GetPlacementAt(anchor) would resolve the wrong
            // placement (and thus the wrong anchor index / orientation) for the persisted event.
            var targetPlacementSnapshot = resultPlacement != null
                ? PlacementSnapshot.FromPlacement(resultPlacement, placementInventory.GetSlot)
                : ResolvePlacementSnapshot(targetInventory, anchorSlot);

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
                targetPlacementSnapshot,
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
            ItemStack subStack,
            out Placement resultPlacement)
        {
            resultPlacement = null;
            if (candidate.Kind == PlacementCandidateKind.Merge)
            {
                if (!targetInventory.TryAddToSlotStack(anchorSlot, subStack))
                    return false;

                // The merge target's own placement is the source of truth for the snapshot;
                // resolving it by the anchor cell is unreliable for complex (interlocking) shapes.
                resultPlacement = candidate.TargetPlacement
                    ?? placementInventory?.GetPlacementAt(anchorSlot);
                return true;
            }

            // Create / NewDynamicSlot: defend against custom strategies by re-validating the
            // footprint against the real topology right before mutation.
            var shape = candidate.Shape ?? PlacementShapeUtility.Resolve(subStack.PrimaryAdapter);
            if (placementInventory != null)
            {
                var placementRequest = new PlacementRequest(subStack, anchorSlot.Index, candidate.Orientation, shape);
                return placementInventory.CanPlace(placementRequest) &&
                       placementInventory.TryPlace(placementRequest, out resultPlacement);
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

                // The converted objects now belong to the target inventory, so they must stop
                // being offered as conversion results for the rest of this drag.
                TransferItemConversionUtility.ConsumeCommitted(
                    transaction.SourceInventory,
                    transaction.TargetInventory,
                    committed.Outcome.SourceRemovedStack?.Adapters,
                    transaction.ConversionSession);

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

        private static async Task<RuleResult> ValidateTransferStartAsync(
            DragContext context,
            IInventory targetInventory,
            CancellationToken cancellationToken)
        {
            var handlers = new HashSet<IAsyncTransferDomainHandler>();

            foreach (var entry in context.Entries)
            {
                var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                if (sourceInventory?.DataBinding is IAsyncTransferDomainHandler sourceHandler)
                    handlers.Add(sourceHandler);
            }

            if (targetInventory?.DataBinding is IAsyncTransferDomainHandler targetHandler)
                handlers.Add(targetHandler);

            foreach (var handler in handlers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var result = await handler.CanStartTransferAsync(
                        context,
                        targetInventory,
                        cancellationToken);
                    if (!result.IsValid)
                    {
                        return RuleResult.Failure(
                            string.IsNullOrEmpty(result.FailureReason)
                                ? "Transfer rejected by async domain handler"
                                : result.FailureReason);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return RuleResult.Failure(ex.Message);
                }
            }

            return RuleResult.Success();
        }

        private static bool HasAsyncTransferStartHandler(
            DragContext context,
            IInventory targetInventory)
        {
            foreach (var entry in context.Entries)
            {
                var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
                if (sourceInventory?.DataBinding is IAsyncTransferDomainHandler)
                    return true;
            }

            return targetInventory?.DataBinding is IAsyncTransferDomainHandler;
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

        /// <summary>
        /// The first cell a placement actually occupies. Reliable as a placement identity/lookup key
        /// even for complex shapes, whose anchor index (bounding-box origin) need not be covered.
        /// </summary>
        private static int ResolvePlacementPrimaryIndex(Placement placement)
        {
            if (placement?.CoveredIndices != null && placement.CoveredIndices.Count > 0)
                return placement.CoveredIndices[0];

            return placement?.AnchorIndex ?? -1;
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
            TransferConversionSession session,
            out IItemAdapter previewAdapter)
        {
            return TransferItemConversionUtility.TryResolveTargetItem(
                sourceInventory,
                targetInventory,
                sourceAdapter,
                session,
                out previewAdapter);
        }

        // ──── Swap ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// True when the explicit target cell is covered by the placement being dragged, so the
        /// only thing "blocking" it is the item's own footprint.
        /// </summary>
        private static bool IsCoveredBySourcePlacement(
            TransferEntryRequest request,
            IInventory sourceInventory,
            IInventory targetInventory)
        {
            if (!ReferenceEquals(sourceInventory, targetInventory) ||
                targetInventory is not IPlacementInventory placementInventory ||
                request.TargetBaseSlot == null)
                return false;

            var sourcePlacement = request.Entry.SourcePlacement
                ?? placementInventory.GetPlacementAt(request.Entry.SourceBaseSlot);
            return sourcePlacement != null &&
                   ReferenceEquals(placementInventory.GetPlacementAt(request.TargetBaseSlot), sourcePlacement);
        }

        private EntryTransferResult TryExecuteSwap(TransferEntryRequest request)
        {
            int requestedAmount = request?.Entry.Stack?.Count ?? 0;
            if (!TryResolveSwap(request, out var swap, out var failureReason))
                return EntryTransferResult.Failed(requestedAmount, failureReason);

            var displacedStacks = new ItemStack[swap.Displacements.Count];
            var displacedSourceSlots = new BaseSlot[swap.Displacements.Count];
            var displacedDestinationSlots = new BaseSlot[swap.Displacements.Count];
            for (int i = 0; i < swap.Displacements.Count; i++)
            {
                displacedStacks[i] = swap.Displacements[i].StackBefore;
                displacedSourceSlots[i] = swap.Displacements[i].SourceSlot;
                displacedDestinationSlots[i] = swap.Displacements[i].DestinationSlot;
            }

            var primary = swap.Displacements[0];
            var swapContext = new InventorySwapContext(
                swap.SourceStackBefore,
                primary.StackBefore,
                swap.SourceSlot,
                swap.TargetSlot,
                swap.SourceInventory,
                swap.TargetInventory,
                displacedStacks,
                displacedSourceSlots,
                displacedDestinationSlots);
            if (request.SwapAttempting != null && !request.SwapAttempting(swapContext))
                return EntryTransferResult.Failed(requestedAmount, "Swap cancelled by listener");

            var sourceSnapshot = swap.SourceSnapshotProvider.CaptureSnapshot();
            var targetSnapshot = ReferenceEquals(swap.SourceInventory, swap.TargetInventory)
                ? null
                : swap.TargetSnapshotProvider.CaptureSnapshot();
            var sourceRemovedSnapshot = PlacementSnapshot.FromPlacement(
                swap.SourcePlacement, swap.SourcePlacementInventory.GetSlot);
            var displacedRemovedSnapshots = new PlacementSnapshot[swap.Displacements.Count];
            for (int i = 0; i < swap.Displacements.Count; i++)
                displacedRemovedSnapshots[i] = PlacementSnapshot.FromPlacement(
                    swap.Displacements[i].Placement, swap.TargetPlacementInventory.GetSlot);

            bool removed = swap.SourcePlacementInventory.RemovePlacement(swap.SourcePlacement);
            for (int i = 0; removed && i < swap.Displacements.Count; i++)
                removed = swap.TargetPlacementInventory.RemovePlacement(swap.Displacements[i].Placement);
            if (!removed)
            {
                RestoreSwapSnapshots(
                    swap.SourceInventory, swap.SourceSnapshotProvider, sourceSnapshot,
                    swap.TargetInventory, swap.TargetSnapshotProvider, targetSnapshot);
                return EntryTransferResult.Failed(requestedAmount, "Swap: failed to vacate placements");
            }

            var forwardRequest = new PlacementRequest(
                swap.ForwardStack.CreateCopy(),
                swap.ForwardAnchor.Index,
                swap.ForwardOrientation,
                swap.ForwardShape);
            if (!swap.TargetPlacementInventory.TryPlace(forwardRequest, out var forwardPlacement))
            {
                RestoreSwapSnapshots(
                    swap.SourceInventory, swap.SourceSnapshotProvider, sourceSnapshot,
                    swap.TargetInventory, swap.TargetSnapshotProvider, targetSnapshot);
                return EntryTransferResult.Failed(requestedAmount, "Swap: cannot place source item in target");
            }

            var reversePlacements = new Placement[swap.Displacements.Count];
            for (int i = 0; i < swap.Displacements.Count; i++)
            {
                var displacement = swap.Displacements[i];
                var reverseRequest = new PlacementRequest(
                    displacement.ConvertedStack.CreateCopy(),
                    displacement.DestinationSlot.Index,
                    displacement.ConvertedOrientation,
                    displacement.ConvertedShape);
                if (swap.SourcePlacementInventory.TryPlace(reverseRequest, out reversePlacements[i]))
                    continue;

                RestoreSwapSnapshots(
                    swap.SourceInventory, swap.SourceSnapshotProvider, sourceSnapshot,
                    swap.TargetInventory, swap.TargetSnapshotProvider, targetSnapshot);
                return EntryTransferResult.Failed(
                    requestedAmount,
                    $"Swap: cannot place displaced item '{displacement.StackBefore.PrimaryAdapter?.ItemId}' in source");
            }

            swap.SourceInventory.UpdateAllVisuals();
            swap.TargetInventory.UpdateAllVisuals();
            var forwardAddedSnapshot = PlacementSnapshot.FromPlacement(
                forwardPlacement, swap.TargetPlacementInventory.GetSlot);
            var reverseAddedSnapshots = new PlacementSnapshot[reversePlacements.Length];
            for (int i = 0; i < reversePlacements.Length; i++)
                reverseAddedSnapshots[i] = PlacementSnapshot.FromPlacement(
                    reversePlacements[i], swap.SourcePlacementInventory.GetSlot);

            var session = request.Context?.ConversionSession;
            TransferItemConversionUtility.ConsumeCommitted(
                swap.SourceInventory, swap.TargetInventory, swap.SourceStackBefore.Adapters, session);
            for (int i = 0; i < swap.Displacements.Count; i++)
                TransferItemConversionUtility.ConsumeCommitted(
                    swap.TargetInventory,
                    swap.SourceInventory,
                    swap.Displacements[i].StackBefore.Adapters,
                    session);

            swap.ForwardDomain.MarkCommitted(
                swap.ForwardAnchor,
                swap.ForwardStack.PrimaryAdapter,
                swap.SourceStackBefore.Count);
            InvokeSwapSuccessHandlers(swap.ForwardDomain);
            for (int i = 0; i < swap.Displacements.Count; i++)
            {
                var displacement = swap.Displacements[i];
                displacement.DomainContext.MarkCommitted(
                    displacement.DestinationSlot,
                    displacement.ConvertedStack.PrimaryAdapter,
                    displacement.StackBefore.Count);
                InvokeSwapSuccessHandlers(displacement.DomainContext);
            }

            DispatchMultiSwapEvents(
                swap,
                sourceRemovedSnapshot,
                displacedRemovedSnapshots,
                forwardAddedSnapshot,
                reverseAddedSnapshots);
            request.SwapCompleted?.Invoke(swapContext);

            var outcome = new PlacementTransferOutcome(
                PlacementTransferOutcomeKind.Swap,
                swap.SourceInventory,
                swap.TargetInventory,
                swap.SourceSlot,
                swap.ForwardAnchor,
                swap.SourceStackBefore,
                swap.ForwardStack,
                sourceRemovedSnapshot,
                forwardAddedSnapshot,
                targetWasEmptyBefore: false);
            return EntryTransferResult.Committed(requestedAmount, requestedAmount, new[] { outcome });
        }

        private bool TryResolveSwap(
            TransferEntryRequest request,
            out ResolvedSwap resolved,
            out string failureReason)
        {
            resolved = null;
            failureReason = "Swap: invalid request";
            var entry = request?.Entry ?? default;
            int requestedAmount = entry.Stack?.Count ?? 0;
            var sourceInventory = entry.SourceInventory ?? entry.SourceBaseSlot?.Inventory;
            var targetInventory = request?.TargetInventory;
            var sourceSlot = entry.SourceBaseSlot;
            var targetSlot = request?.TargetBaseSlot;

            if (sourceInventory == null || targetInventory == null || sourceSlot == null || targetSlot == null)
                return false;
            if (sourceSlot.IsEmpty)
            {
                failureReason = "Swap requires a non-empty source";
                return false;
            }
            if (sourceSlot.Stack?.Count != requestedAmount)
            {
                failureReason = "Swap requires the entire source stack";
                return false;
            }
            if (sourceInventory is not IInventorySnapshotProvider sourceSnapshotProvider ||
                targetInventory is not IInventorySnapshotProvider targetSnapshotProvider)
            {
                failureReason = "Swap requires snapshot-capable inventories";
                return false;
            }
            if (sourceInventory is not IPlacementInventory sourcePlacementInventory ||
                targetInventory is not IPlacementInventory targetPlacementInventory)
            {
                failureReason = "Swap requires placement-capable inventories";
                return false;
            }

            var sourcePlacement = sourcePlacementInventory.GetPlacementAt(sourceSlot);
            if (sourcePlacement?.Stack == null)
            {
                failureReason = "Swap: cannot resolve placements";
                return false;
            }

            // What sits under the pointer is only used to pick which displaced item counts as the
            // primary one for the legacy TargetStack/TargetBaseSlot fields. It never decides whether
            // this is a swap: with a multi-cell footprint the pointer cell depends on where the item
            // was grabbed, and the same drop would otherwise succeed or fail based on that alone.
            var hoveredTargetPlacement = targetSlot.IsEmpty
                ? null
                : targetPlacementInventory.GetPlacementAt(targetSlot);
            bool hoveredSourcePlacement =
                hoveredTargetPlacement != null &&
                ReferenceEquals(sourceInventory, targetInventory) &&
                ReferenceEquals(sourcePlacement, hoveredTargetPlacement);
            var primaryTargetPlacement = hoveredSourcePlacement ? null : hoveredTargetPlacement;

            var session = request.Context?.ConversionSession;
            var sourceStackBefore = sourcePlacement.Stack.CreateCopy();
            if (!ItemStack.TryCreate(sourcePlacement.Stack.Adapters, out var forwardStack) ||
                !TransferItemConversionUtility.TryConvertStackToTargetDomain(
                    sourceInventory, targetInventory, forwardStack, session))
            {
                failureReason = "Swap: forward conversion failed";
                return false;
            }

            var forwardShape = PlacementShapeUtility.Resolve(forwardStack.PrimaryAdapter)
                ?? sourcePlacement.Shape;
            int forwardOrientation = OrientationStepUtility.Project(
                entry.OrientationTopology,
                entry.Orientation,
                targetPlacementInventory.Topology);
            var geometry = new InventoryPlacementGeometry(targetInventory);
            BaseSlot forwardAnchor;
            List<Placement> displaced;
            IReadOnlyList<BaseSlot> forwardCoveredSlots;

            // The anchor decides where the incoming item lands and at which orientation — nothing
            // else. It is always the one the pointer resolves to, exactly like an ordinary shaped
            // drop. What the swap displaces is then read off the cells that anchor actually covers,
            // so the decision never depends on where the item underneath happens to be anchored.
            var acceptance = new InventoryAcceptanceRequest(
                targetInventory,
                forwardStack.PrimaryAdapter,
                sourceStackBefore.Count,
                request.Context.WithTarget(targetSlot, targetInventory),
                entry);
            if (!geometry.TryResolveAnchor(targetSlot, acceptance, out forwardAnchor) ||
                !TryCollectDisplacedPlacements(
                    geometry, forwardAnchor, forwardShape, forwardOrientation,
                    sourceInventory, targetInventory, sourcePlacement,
                    out displaced, out forwardCoveredSlots))
            {
                failureReason = "Swap: cannot resolve the target footprint";
                return false;
            }

            if (forwardAnchor == null)
            {
                failureReason = "Swap: cannot resolve the target footprint";
                return false;
            }
            if (displaced.Count == 0)
            {
                failureReason = "Swap: incoming footprint does not displace another placement";
                return false;
            }

            // The caller judged the drop against the cell under the pointer alone. Judge it against
            // every cell the item will occupy: a cell that forbids the item forbids it whether the
            // item arrives there with its anchor or with its tail.
            var forwardEvaluator = new RuleEvaluationService();
            for (int i = 0; i < forwardCoveredSlots.Count; i++)
            {
                var coveredSlot = forwardCoveredSlots[i];
                if (ReferenceEquals(coveredSlot, targetSlot))
                    continue;

                var forwardRules = forwardEvaluator.ValidateEntryDrop(
                    request.Context.WithTarget(coveredSlot, targetInventory),
                    entry,
                    request.GlobalRules);
                if (!forwardRules.IsValid)
                {
                    failureReason = string.IsNullOrEmpty(forwardRules.FailureReason)
                        ? "Swap: drop rules rejected the item on a cell it covers"
                        : $"Swap: {forwardRules.FailureReason}";
                    return false;
                }
            }

            // The pointer may rest on a cell the footprint covers but that belongs to nothing, or to
            // the dragged item itself. Then the first displaced placement in footprint order stands
            // in as the primary one, so the legacy single-swap fields always describe a real item.
            if (!ContainsPlacement(displaced, primaryTargetPlacement))
                primaryTargetPlacement = displaced[0];
            if (request.Policy.SwapDisplacementMode == SwapDisplacementMode.SinglePlacement && displaced.Count > 1)
            {
                failureReason = "Swap: target footprint covers multiple items";
                return false;
            }

            resolved = new ResolvedSwap
            {
                SourceInventory = sourceInventory,
                TargetInventory = targetInventory,
                SourcePlacementInventory = sourcePlacementInventory,
                TargetPlacementInventory = targetPlacementInventory,
                SourceSnapshotProvider = sourceSnapshotProvider,
                TargetSnapshotProvider = targetSnapshotProvider,
                SourceSlot = sourceSlot,
                TargetSlot = hoveredSourcePlacement
                    ? targetPlacementInventory.GetSlot(ResolvePlacementPrimaryIndex(primaryTargetPlacement))
                    : targetSlot,
                ForwardAnchor = forwardAnchor,
                SourcePlacement = sourcePlacement,
                PrimaryTargetPlacement = primaryTargetPlacement,
                SourceStackBefore = sourceStackBefore,
                ForwardStack = forwardStack,
                ForwardShape = forwardShape,
                ForwardOrientation = forwardOrientation,
                ForwardCoveredSlots = forwardCoveredSlots
            };

            // Items of different shapes never cover each other exactly, so a swap that only clips a
            // neighbour is the common case. Reject keeps swaps to clean exchanges: every displaced
            // item must sit entirely under the incoming footprint.
            var partialOverlap = request.Policy.PartialOverlapSwap;
            if (partialOverlap == PartialOverlapSwapMode.Reject)
            {
                var coveredCells = new HashSet<int>();
                for (int i = 0; i < forwardCoveredSlots.Count; i++)
                    coveredCells.Add(forwardCoveredSlots[i].Index);

                var incomingOffsets = targetPlacementInventory.Topology
                    .GetPlacementOffsets(forwardShape, forwardOrientation);

                for (int i = 0; i < displaced.Count; i++)
                {
                    if (IsFullyCovered(displaced[i], coveredCells))
                        continue;

                    // An item smaller than the one under it can never cover it whole, however
                    // carefully it is aimed. Refusing those would ban every small-onto-large swap,
                    // so Reject only fires where a clean exchange was actually achievable.
                    var displacedOffsets = targetPlacementInventory.Topology
                        .GetPlacementOffsets(displaced[i].Shape, displaced[i].Orientation);
                    if (!CanContainShape(incomingOffsets, displacedOffsets))
                        continue;

                    failureReason =
                        $"Swap: '{displaced[i].Stack?.ID}' is only partly covered by the incoming footprint";
                    resolved = null;
                    return false;
                }
            }

            var targetAnchorCell = targetPlacementInventory.Topology.ToCell(forwardAnchor.Index);
            // Cells the swap frees in the source inventory. They are the first place a displaced
            // item looks when the mode searches rather than trusting the grab offset.
            if (partialOverlap == PartialOverlapSwapMode.VacatedArea)
                PrepareDisplacementSearch(resolved, displaced);
            var displacementResolutionOrder = CreateDisplacementResolutionOrder(
                displaced,
                partialOverlap);
            for (int i = 0; i < displacementResolutionOrder.Count; i++)
            {
                var placement = displacementResolutionOrder[i];
                var desiredDestinationCell = sourcePlacement.AnchorCell +
                    (placement.AnchorCell - targetAnchorCell);

                var originSlot = targetPlacementInventory.GetSlot(ResolvePlacementPrimaryIndex(placement));
                var stackBefore = placement.Stack.CreateCopy();
                if (originSlot == null ||
                    !ItemStack.TryCreate(placement.Stack.Adapters, out var convertedStack) ||
                    !TransferItemConversionUtility.TryConvertStackToTargetDomain(
                        targetInventory, sourceInventory, convertedStack, session))
                {
                    failureReason = "Swap: reverse conversion failed";
                    resolved = null;
                    return false;
                }

                var convertedShape = PlacementShapeUtility.Resolve(convertedStack.PrimaryAdapter)
                    ?? placement.Shape;
                float visualAngle = targetPlacementInventory.Topology
                    .GetVisualAngleDegrees(placement.Orientation);
                int convertedOrientation = sourcePlacementInventory.Topology
                    .GetOrientationForVisualAngleDegrees(visualAngle);
                var ruleTargetPlacement = placement;
                var ruleOriginSlot = originSlot;
                var ruleShape = convertedShape;
                int ruleOrientation = convertedOrientation;
                bool IsAcceptableDestination(BaseSlot candidate) =>
                    ValidateSwapCounterpartAt(
                        request.Context, request.GlobalRules,
                        sourceInventory, targetInventory,
                        candidate, ruleOriginSlot, ruleTargetPlacement,
                        ruleShape, ruleOrientation).IsValid;

                bool destinationFound = partialOverlap == PartialOverlapSwapMode.VacatedArea
                    ? TryFindSearchedDestination(
                        resolved, desiredDestinationCell, convertedShape, convertedOrientation,
                        IsAcceptableDestination, out var destinationSlot)
                    : TryResolveDisplacedDestination(
                        resolved, desiredDestinationCell, convertedShape, convertedOrientation,
                        out destinationSlot);
                if (!destinationFound)
                {
                    failureReason = partialOverlap == PartialOverlapSwapMode.VacatedArea
                        ? $"Swap: nowhere to put the displaced item '{placement.Stack?.ID}'"
                        : "Swap: displaced destination is outside or overlaps the incoming footprint";
                    resolved = null;
                    return false;
                }
                var rules = ValidateSwapCounterpartAt(
                    request.Context, request.GlobalRules,
                    sourceInventory, targetInventory,
                    destinationSlot, originSlot, placement,
                    convertedShape, convertedOrientation);
                if (!rules.IsValid)
                {
                    failureReason = string.IsNullOrEmpty(rules.FailureReason)
                        ? "Swap: counterpart rules rejected the item"
                        : $"Swap: {rules.FailureReason}";
                    resolved = null;
                    return false;
                }

                resolved.Displacements.Add(new ResolvedSwapDisplacement
                {
                    Placement = placement,
                    SourceSlot = originSlot,
                    DestinationSlot = destinationSlot,
                    StackBefore = stackBefore,
                    ConvertedStack = convertedStack,
                    ConvertedShape = convertedShape,
                    ConvertedOrientation = convertedOrientation
                });

                // Whatever this item took is no longer free for the ones resolved after it.
                if (resolved.VacatedCells != null)
                {
                    var claimed = sourcePlacementInventory.GetCoveredCells(
                        destinationSlot.Index, convertedShape, convertedOrientation);
                    if (claimed != null)
                        for (int c = 0; c < claimed.Count; c++)
                        {
                            resolved.VacatedCells.Remove(claimed[c]);
                            resolved.ClaimedCells.Add(claimed[c]);
                        }
                }
            }

            // Primary is a legacy callback projection, not a placement priority. Reorder only after
            // every destination has been resolved so the cell under the pointer cannot change which
            // displacement gets first claim on the vacated area.
            MovePrimaryFirst(resolved.Displacements, primaryTargetPlacement);

            if (!ValidateResolvedSwapGeometry(resolved, out failureReason))
            {
                resolved = null;
                return false;
            }

            resolved.ForwardDomain = new TransferDomainContext(
                sourceInventory, targetInventory, sourceSlot, forwardAnchor,
                sourceStackBefore.PrimaryAdapter, forwardStack.PrimaryAdapter,
                sourceStackBefore.Count, TransferKind.Swap);
            var reverseDomains = new TransferDomainContext[resolved.Displacements.Count];
            for (int i = 0; i < resolved.Displacements.Count; i++)
            {
                var displacement = resolved.Displacements[i];
                var domain = new TransferDomainContext(
                    targetInventory, sourceInventory,
                    displacement.SourceSlot, displacement.DestinationSlot,
                    displacement.StackBefore.PrimaryAdapter,
                    displacement.ConvertedStack.PrimaryAdapter,
                    displacement.StackBefore.Count, TransferKind.Swap);
                displacement.DomainContext = domain;
                reverseDomains[i] = domain;
            }

            resolved.ForwardDomain.CounterpartContexts = reverseDomains;
            resolved.ForwardDomain.CounterpartContext = reverseDomains[0];
            for (int i = 0; i < reverseDomains.Length; i++)
            {
                reverseDomains[i].CounterpartContext = resolved.ForwardDomain;
                reverseDomains[i].CounterpartContexts = new[] { resolved.ForwardDomain };
            }

            if (!ValidateDomainHandlers(resolved.ForwardDomain))
            {
                failureReason = "Swap: forward domain validation failed";
                resolved = null;
                return false;
            }
            for (int i = 0; i < reverseDomains.Length; i++)
            {
                if (ValidateDomainHandlers(reverseDomains[i]))
                    continue;
                failureReason = "Swap: reverse domain validation failed";
                resolved = null;
                return false;
            }

            failureReason = null;
            return true;
        }

        private static bool TryResolveDisplacedDestination(
            ResolvedSwap swap,
            Vector2Int desiredCell,
            IPlacementShape shape,
            int orientation,
            out BaseSlot destinationSlot)
        {
            destinationSlot = null;
            var topology = swap?.SourcePlacementInventory?.Topology;
            if (topology == null)
                return false;

            bool sameInventory = ReferenceEquals(swap.SourceInventory, swap.TargetInventory);
            if (!sameInventory)
            {
                if (!topology.TryToIndex(desiredCell, out int destinationIndex))
                    return false;
                destinationSlot = swap.SourcePlacementInventory.GetSlot(destinationIndex);
                return destinationSlot != null;
            }

            var shift = swap.SourcePlacement.AnchorCell -
                swap.TargetPlacementInventory.Topology.ToCell(swap.ForwardAnchor.Index);
            var incomingCells = new HashSet<int>();
            for (int i = 0; i < swap.ForwardCoveredSlots.Count; i++)
                incomingCells.Add(swap.ForwardCoveredSlots[i].Index);

            bool shiftedForOverlap = false;
            int attempts = Math.Max(1, topology.CellCount);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (!topology.TryToIndex(desiredCell, out int destinationIndex))
                    return false;
                var covered = swap.SourcePlacementInventory.GetCoveredCells(
                    destinationIndex, shape, orientation);
                if (covered == null || covered.Count == 0)
                    return false;

                bool overlapsIncoming = false;
                for (int i = 0; i < covered.Count; i++)
                {
                    if (!incomingCells.Contains(covered[i]))
                        continue;
                    overlapsIncoming = true;
                    break;
                }

                if (!overlapsIncoming)
                {
                    // An overlapping same-inventory move can translate a counterpart into a cell
                    // still occupied by the incoming shape. After pushing through that overlap,
                    // keep the counterpart inside the part of the original source footprint that
                    // is actually being vacated.
                    if (shiftedForOverlap)
                    {
                        for (int i = 0; i < covered.Count; i++)
                            if (!ContainsIndex(swap.SourcePlacement.CoveredIndices, covered[i]))
                                return false;
                    }
                    destinationSlot = swap.SourcePlacementInventory.GetSlot(destinationIndex);
                    return destinationSlot != null;
                }

                if (shift == Vector2Int.zero)
                    return false;
                desiredCell += shift;
                shiftedForOverlap = true;
            }
            return false;
        }

        /// <summary>
        /// Fills the state the displacement search needs: which placements are about to disappear,
        /// which cells the incoming item will take, and which cells the swap therefore frees.
        /// </summary>
        private static void PrepareDisplacementSearch(ResolvedSwap swap, List<Placement> displaced)
        {
            bool sameInventory = ReferenceEquals(swap.SourceInventory, swap.TargetInventory);

            var removed = new HashSet<Placement> { swap.SourcePlacement };
            var incoming = new HashSet<int>();
            var vacated = new HashSet<int>();

            var sourceCovered = swap.SourcePlacement.CoveredIndices;
            for (int i = 0; i < sourceCovered.Count; i++)
                vacated.Add(sourceCovered[i]);

            // Displaced placements live in the target inventory, so their cells and the incoming
            // footprint only mean anything on this side when both sides are the same inventory.
            if (sameInventory)
            {
                for (int i = 0; i < displaced.Count; i++)
                {
                    removed.Add(displaced[i]);
                    var covered = displaced[i].CoveredIndices;
                    for (int c = 0; c < covered.Count; c++)
                        vacated.Add(covered[c]);
                }

                for (int i = 0; i < swap.ForwardCoveredSlots.Count; i++)
                {
                    incoming.Add(swap.ForwardCoveredSlots[i].Index);
                    vacated.Remove(swap.ForwardCoveredSlots[i].Index);
                }
            }

            swap.RemovedPlacements = removed;
            swap.IncomingCells = incoming;
            swap.VacatedCells = vacated;
        }

        /// <summary>
        /// Whether <paramref name="inner"/> fits entirely inside <paramref name="outer"/> under some
        /// alignment — a question about the two shapes, not about where they currently sit. Cell
        /// counts alone would not answer it: a 3x1 has as many cells as an L of three, yet can never
        /// contain it.
        /// </summary>
        private static bool CanContainShape(
            IReadOnlyList<Vector2Int> outer,
            IReadOnlyList<Vector2Int> inner)
        {
            if (outer == null || inner == null || inner.Count == 0 || inner.Count > outer.Count)
                return false;

            var outerCells = new HashSet<Vector2Int>(outer);
            for (int i = 0; i < outer.Count; i++)
            {
                var delta = outer[i] - inner[0];
                bool contained = true;
                for (int j = 0; j < inner.Count; j++)
                {
                    if (outerCells.Contains(inner[j] + delta))
                        continue;
                    contained = false;
                    break;
                }
                if (contained)
                    return true;
            }

            return false;
        }

        /// <summary>True when every cell of <paramref name="placement"/> lies under the footprint.</summary>
        private static bool IsFullyCovered(Placement placement, HashSet<int> footprintCells)
        {
            var covered = placement?.CoveredIndices;
            if (covered == null || covered.Count == 0)
                return false;
            for (int i = 0; i < covered.Count; i++)
                if (!footprintCells.Contains(covered[i]))
                    return false;
            return true;
        }

        /// <summary>
        /// A cell a displaced item may occupy: not taken by the incoming item, not already claimed
        /// by an earlier displacement, and either empty or held by a placement this swap removes.
        /// </summary>
        private static bool IsCellFreeForDisplacement(ResolvedSwap swap, int cellIndex)
        {
            if (swap.IncomingCells.Contains(cellIndex) || swap.ClaimedCells.Contains(cellIndex))
                return false;
            var existing = swap.SourcePlacementInventory.GetPlacementAt(cellIndex);
            return existing == null || swap.RemovedPlacements.Contains(existing);
        }

        /// <summary>
        /// Best free position for a displaced item. Every candidate anchor is tested the way any
        /// placement is tested — project the footprint with <c>GetCoveredCells</c> and check each
        /// cell — then ranked: positions lying entirely in the freed area win, then the ones leaning
        /// on it with the most cells, and only then proximity to where the grab offset pointed.
        /// <para>
        /// Positions the item's own rules refuse are skipped instead of failing the swap, so this
        /// takes a predicate rather than leaving validation to the caller. Candidates are walked in
        /// index order, so equal-ranking positions always resolve to the lowest cell index.
        /// </para>
        /// </summary>
        private static bool TryFindSearchedDestination(
            ResolvedSwap swap,
            Vector2Int preferredCell,
            IPlacementShape shape,
            int orientation,
            Func<BaseSlot, bool> isAcceptable,
            out BaseSlot destinationSlot)
        {
            destinationSlot = null;
            var inventory = swap?.SourcePlacementInventory;
            var topology = inventory?.Topology;
            if (topology == null || swap.VacatedCells == null)
                return false;

            int bestOutside = int.MaxValue;
            int bestDistance = int.MaxValue;

            for (int anchorIndex = 0; anchorIndex < topology.CellCount; anchorIndex++)
            {
                var covered = inventory.GetCoveredCells(anchorIndex, shape, orientation);
                if (covered == null || covered.Count == 0)
                    continue;

                bool fits = true;
                int outside = 0;
                for (int i = 0; i < covered.Count; i++)
                {
                    if (!IsCellFreeForDisplacement(swap, covered[i]))
                    {
                        fits = false;
                        break;
                    }
                    if (!swap.VacatedCells.Contains(covered[i]))
                        outside++;
                }
                if (!fits)
                    continue;

                var cell = topology.ToCell(anchorIndex);
                int distance = Math.Abs(cell.x - preferredCell.x) + Math.Abs(cell.y - preferredCell.y);
                if (outside > bestOutside || (outside == bestOutside && distance >= bestDistance))
                    continue;

                var slot = inventory.GetSlot(anchorIndex);
                if (slot == null || (isAcceptable != null && !isAcceptable(slot)))
                    continue;

                bestOutside = outside;
                bestDistance = distance;
                destinationSlot = slot;
            }

            return destinationSlot != null;
        }

        private static bool ContainsIndex(IReadOnlyList<int> indices, int expected)
        {
            if (indices == null)
                return false;
            for (int i = 0; i < indices.Count; i++)
                if (indices[i] == expected)
                    return true;
            return false;
        }

        private static bool TryCollectDisplacedPlacements(
            InventoryPlacementGeometry geometry,
            BaseSlot anchorSlot,
            IPlacementShape shape,
            int orientation,
            IInventory sourceInventory,
            IInventory targetInventory,
            Placement sourcePlacement,
            out List<Placement> displaced,
            out IReadOnlyList<BaseSlot> coveredSlots)
        {
            displaced = new List<Placement>();
            coveredSlots = geometry.GetCoveredSlots(anchorSlot, shape, orientation);
            if (coveredSlots == null || coveredSlots.Count == 0)
                return false;

            var seen = new HashSet<Placement>();
            for (int i = 0; i < coveredSlots.Count; i++)
            {
                var placement = geometry.GetPlacementAt(coveredSlots[i]);
                if (placement == null ||
                    ReferenceEquals(sourceInventory, targetInventory) &&
                    ReferenceEquals(placement, sourcePlacement) ||
                    !seen.Add(placement))
                    continue;
                displaced.Add(placement);
            }
            return true;
        }

        private static bool ContainsPlacement(IReadOnlyList<Placement> placements, Placement expected)
        {
            if (placements == null || expected == null)
                return false;
            for (int i = 0; i < placements.Count; i++)
                if (ReferenceEquals(placements[i], expected))
                    return true;
            return false;
        }

        /// <summary>
        /// Produces a deterministic mutation-independent resolution order. Vacated-area placement
        /// is greedy by policy, so larger footprints go first to avoid a one-cell item fragmenting
        /// the only region that can hold a later shaped item. Equal-size placements retain topology
        /// traversal order.
        /// </summary>
        private static List<Placement> CreateDisplacementResolutionOrder(
            IReadOnlyList<Placement> placements,
            PartialOverlapSwapMode partialOverlap)
        {
            var result = placements == null
                ? new List<Placement>()
                : new List<Placement>(placements);
            if (partialOverlap != PartialOverlapSwapMode.VacatedArea)
                return result;

            // Stable insertion sort: displacement counts are normally tiny, and avoiding a
            // comparer allocation matters because the same resolver also runs during Probe.
            for (int i = 1; i < result.Count; i++)
            {
                var current = result[i];
                int currentSize = current?.CoveredIndices?.Count ?? 0;
                int destination = i;
                while (destination > 0)
                {
                    int previousSize = result[destination - 1]?.CoveredIndices?.Count ?? 0;
                    if (previousSize >= currentSize)
                        break;
                    result[destination] = result[destination - 1];
                    destination--;
                }
                result[destination] = current;
            }

            return result;
        }

        private static void MovePrimaryFirst(
            List<ResolvedSwapDisplacement> displacements,
            Placement primary)
        {
            for (int i = 0; i < displacements.Count; i++)
            {
                if (!ReferenceEquals(displacements[i].Placement, primary) || i == 0)
                    continue;
                var primaryDisplacement = displacements[i];
                displacements.RemoveAt(i);
                displacements.Insert(0, primaryDisplacement);
                return;
            }
        }

        private static bool ValidateResolvedSwapGeometry(ResolvedSwap swap, out string failureReason)
        {
            var sourceRemoved = new HashSet<Placement> { swap.SourcePlacement };
            var targetRemoved = new HashSet<Placement>();
            for (int i = 0; i < swap.Displacements.Count; i++)
                targetRemoved.Add(swap.Displacements[i].Placement);
            if (ReferenceEquals(swap.SourceInventory, swap.TargetInventory))
            {
                foreach (var placement in targetRemoved)
                    sourceRemoved.Add(placement);
                targetRemoved = sourceRemoved;
            }

            var sourceReserved = new HashSet<int>();
            var targetReserved = ReferenceEquals(swap.SourceInventory, swap.TargetInventory)
                ? sourceReserved
                : new HashSet<int>();
            if (!TryReserveSwapFootprint(
                    swap.TargetPlacementInventory, swap.ForwardAnchor,
                    swap.ForwardShape, swap.ForwardOrientation,
                    targetRemoved, targetReserved))
            {
                failureReason = "Swap: source item cannot occupy the target footprint";
                return false;
            }

            for (int i = 0; i < swap.Displacements.Count; i++)
            {
                var displacement = swap.Displacements[i];
                if (TryReserveSwapFootprint(
                        swap.SourcePlacementInventory, displacement.DestinationSlot,
                        displacement.ConvertedShape, displacement.ConvertedOrientation,
                        sourceRemoved, sourceReserved))
                    continue;
                failureReason =
                    $"Swap: displaced item '{displacement.StackBefore.PrimaryAdapter?.ItemId}' cannot occupy its destination";
                return false;
            }

            failureReason = null;
            return true;
        }

        private static bool TryReserveSwapFootprint(
            IPlacementInventory inventory,
            BaseSlot anchor,
            IPlacementShape shape,
            int orientation,
            HashSet<Placement> removed,
            HashSet<int> reserved)
        {
            if (inventory == null || anchor == null)
                return false;
            var covered = inventory.GetCoveredCells(anchor.Index, shape, orientation);
            if (covered == null || covered.Count == 0)
                return false;
            for (int i = 0; i < covered.Count; i++)
            {
                int index = covered[i];
                if (reserved.Contains(index))
                    return false;
                var existing = inventory.GetPlacementAt(index);
                if (existing != null && (removed == null || !removed.Contains(existing)))
                    return false;
            }
            for (int i = 0; i < covered.Count; i++)
                reserved.Add(covered[i]);
            return true;
        }

        private static RuleResult ValidateSwapCounterpartAt(
            DragContext context,
            GlobalRuleValidator globalRules,
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot destinationSlot,
            BaseSlot originSlot,
            Placement targetPlacement,
            IPlacementShape destinationShape,
            int destinationOrientation)
        {
            var counterpartStack = targetPlacement?.Stack?.CreateCopy();
            if (counterpartStack == null || counterpartStack.IsEmpty)
                return RuleResult.Failure("counterpart stack is empty");
            var counterpartEntry = new DragEntry(
                counterpartStack, originSlot, targetInventory, targetPlacement);

            var evaluator = new RuleEvaluationService();
            var startContext = BuildCounterpartContext(
                context, counterpartEntry, counterpartStack, originSlot,
                targetInventory, destinationSlot, sourceInventory);
            var startResult = evaluator.ValidateEntryStart(startContext, counterpartEntry, globalRules);
            if (!startResult.IsValid)
                return startResult;

            // Judged on every cell it would occupy, so a rule sitting on a cell the item merely
            // covers counts exactly as much as one on the cell it anchors to.
            foreach (var slot in EnumerateDestinationSlots(
                         sourceInventory, destinationSlot, destinationShape, destinationOrientation))
            {
                var dropContext = BuildCounterpartContext(
                    context, counterpartEntry, counterpartStack, originSlot,
                    targetInventory, slot, sourceInventory);
                var dropResult = evaluator.ValidateEntryDrop(dropContext, counterpartEntry, globalRules);
                if (!dropResult.IsValid)
                    return dropResult;
            }

            return RuleResult.Success();
        }

        private static DragContext BuildCounterpartContext(
            DragContext context,
            DragEntry counterpartEntry,
            ItemStack counterpartStack,
            BaseSlot originSlot,
            IInventory targetInventory,
            BaseSlot destinationSlot,
            IInventory sourceInventory)
        {
            return context != null
                ? context.CreateDerived(new[] { counterpartEntry })
                    .WithTarget(destinationSlot, sourceInventory)
                : new DragContext(
                    counterpartStack, originSlot, targetInventory,
                    destinationSlot, sourceInventory);
        }

        private static IEnumerable<BaseSlot> EnumerateDestinationSlots(
            IInventory sourceInventory,
            BaseSlot destinationSlot,
            IPlacementShape destinationShape,
            int destinationOrientation)
        {
            if (destinationSlot == null)
                yield break;

            var covered = sourceInventory is IPlacementInventory placementInventory
                ? placementInventory.GetCoveredCells(
                    destinationSlot.Index, destinationShape, destinationOrientation)
                : null;

            if (covered == null || covered.Count == 0)
            {
                yield return destinationSlot;
                yield break;
            }

            for (int i = 0; i < covered.Count; i++)
            {
                var slot = ((IPlacementInventory)sourceInventory).GetSlot(covered[i]);
                if (slot != null)
                    yield return slot;
            }
        }

        private static void InvokeSwapSuccessHandlers(TransferDomainContext context)
        {
            foreach (var handler in EnumerateDomainHandlers(context))
            {
                try { handler.OnTransferSucceeded(context); }
                catch (Exception ex)
                {
                    Extensions.DragAndDropLog(
                        $"<color=red>[InventoryTransferService] Swap domain hook threw: {ex.Message}</color>");
                }
            }
        }

        private static void DispatchMultiSwapEvents(
            ResolvedSwap swap,
            PlacementSnapshot sourceRemovedSnapshot,
            IReadOnlyList<PlacementSnapshot> displacedRemovedSnapshots,
            PlacementSnapshot forwardAddedSnapshot,
            IReadOnlyList<PlacementSnapshot> reverseAddedSnapshots)
        {
            if (swap.TargetInventory is IInventoryEventSink targetSink)
            {
                for (int i = 0; i < swap.Displacements.Count; i++)
                {
                    var displacement = swap.Displacements[i];
                    targetSink.EmitItemRemoved(
                        displacement.StackBefore, displacement.SourceSlot.Index,
                        swap.SourceInventory, displacement.SourceSlot,
                        displacement.DestinationSlot, displacedRemovedSnapshots[i]);
                }
                targetSink.EmitItemAdded(
                    swap.ForwardStack, swap.ForwardAnchor.Index,
                    swap.SourceInventory, swap.SourceSlot,
                    swap.ForwardAnchor, forwardAddedSnapshot);
            }

            if (swap.SourceInventory is IInventoryEventSink sourceSink)
            {
                sourceSink.EmitItemRemoved(
                    swap.SourceStackBefore, swap.SourceSlot.Index,
                    swap.TargetInventory, swap.SourceSlot,
                    swap.ForwardAnchor, sourceRemovedSnapshot);
                for (int i = 0; i < swap.Displacements.Count; i++)
                {
                    var displacement = swap.Displacements[i];
                    sourceSink.EmitItemAdded(
                        displacement.ConvertedStack, displacement.DestinationSlot.Index,
                        swap.TargetInventory, displacement.SourceSlot,
                        displacement.DestinationSlot, reverseAddedSnapshots[i]);
                }
            }
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
                0,
                Vector2Int.one,
                new[] { slot.Index },
                slot,
                new[] { slot },
                coveredOffsets: new[] { Vector2Int.zero });
        }
    }
}
