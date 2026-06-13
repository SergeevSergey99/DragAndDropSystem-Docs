using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
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
            PlacementCandidateOrderer ordererOverride = null)
        {
            Context = context;
            Entry = entry;
            TargetInventory = targetInventory;
            TargetBaseSlot = targetBaseSlot;
            Policy = policy;
            OrdererOverride = ordererOverride;
        }

        public DragContext Context { get; }
        public DragEntry Entry { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlot { get; }
        public ResolvedDropPolicy Policy { get; }
        public PlacementCandidateOrderer OrdererOverride { get; }
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
                            return EntryTransferResult.Failed(requestedAmount, "Swap is not supported by the JIT pipeline yet");
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
                ReleaseDynamicSlot(targetInventory, createdDynamicSlot);
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
                ReleaseDynamicSlot(targetInventory, createdDynamicSlot);
                return false;
            }

            // Split exactly the candidate amount straight from the source slot: the remainder never
            // leaves the source, so a partial entry cannot orphan items and the source footprint
            // stays occupied for as long as anything remains in it (plan §5.3/§5.5).
            if (!sourceInventory.TrySplitFromSlot(sourceSlot, amount, out var subStack) ||
                subStack == null || subStack.Count != amount)
            {
                ReleaseDynamicSlot(targetInventory, createdDynamicSlot);
                return false;
            }

            var sourceRemovedStack = subStack.CreateCopy();

            if (!TransferItemConversionUtility.TryConvertOutgoingStack(sourceInventory, subStack) ||
                !TransferItemConversionUtility.TryConvertIncomingStack(targetInventory, subStack))
            {
                RestoreSubStack(transaction, sourceRemovedStack);
                ReleaseDynamicSlot(targetInventory, createdDynamicSlot);
                return false;
            }

            var transferredStack = subStack.CreateCopy();

            if (!TryMutateTarget(candidate, targetInventory, placementInventory, geometry, anchorSlot, subStack))
            {
                RestoreSubStack(transaction, sourceRemovedStack);
                ReleaseDynamicSlot(targetInventory, createdDynamicSlot);
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

        private static void RestoreSubStack(EntryTransaction transaction, ItemStack sourceRemovedStack)
        {
            var sourceSlot = transaction.SourceBaseSlot;
            bool restored = sourceSlot.IsEmpty
                ? transaction.SourceInventory.TrySetStackForSlot(sourceSlot, sourceRemovedStack)
                : transaction.SourceInventory.TryAddToSlotStack(sourceSlot, sourceRemovedStack);

            if (!restored)
            {
                // Cannot put the sub-stack back: hard-restore both inventories. The committed list
                // is cleared so the caller fails the entry without dispatching stale events.
                Extensions.DragAndDropLog("<color=red>[InventoryTransferService] Sub-stack restore failed, rolling back entry</color>");
                RestoreSnapshots(transaction);
                transaction.Committed.Clear();
                transaction.Remaining = transaction.RequestedAmount;
                transaction.Aborted = true;
            }

            sourceSlot.UpdateVisuals();
        }

        private static void ReleaseDynamicSlot(IInventory targetInventory, BaseSlot createdSlot)
        {
            if (createdSlot != null && createdSlot.IsEmpty &&
                targetInventory is IDynamicSlotLifecycle lifecycle)
                lifecycle.HandleSlotEmptied(createdSlot);
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
