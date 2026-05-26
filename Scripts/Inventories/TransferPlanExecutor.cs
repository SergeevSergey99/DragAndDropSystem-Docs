using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UDND.Core;
using UDND.Rules;
using UDND.Slots;
using UDND.Tools;

namespace UDND.Inventories
{
    public readonly struct ExecutedTransferEntry
    {
        public ExecutedTransferEntry(
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            IItemAdapter itemAdapter,
            int amount,
            PlacementSnapshot targetPlacementSnapshot = null,
            PlacementSnapshot sourcePlacementSnapshot = null)
        {
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            ItemAdapter = itemAdapter;
            Amount = amount;
            TargetPlacementSnapshot = targetPlacementSnapshot;
            SourcePlacementSnapshot = sourcePlacementSnapshot;
        }

        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }
        public IItemAdapter ItemAdapter { get; }
        public int Amount { get; }
        public PlacementSnapshot SourcePlacementSnapshot { get; }
        public PlacementSnapshot TargetPlacementSnapshot { get; }
        public PlacementSnapshot PlacementSnapshot => TargetPlacementSnapshot;
        public BaseSlot AnchorSlot => TargetPlacementSnapshot?.AnchorBaseSlot ?? TargetBaseSlot;
        public IReadOnlyList<BaseSlot> CoveredSlots => TargetPlacementSnapshot?.CoveredBaseSlots ?? Array.Empty<BaseSlot>();
        public IReadOnlyList<int> CoveredIndices => TargetPlacementSnapshot?.CoveredIndices ?? Array.Empty<int>();
        public IReadOnlyList<Vector2Int> CoveredOffsets => TargetPlacementSnapshot?.CoveredOffsets ?? Array.Empty<Vector2Int>();
        public int AnchorIndex => TargetPlacementSnapshot != null && TargetPlacementSnapshot.AnchorIndex >= 0
            ? TargetPlacementSnapshot.AnchorIndex
            : TargetBaseSlot?.Index ?? -1;
        public PlacementOrientation Orientation => TargetPlacementSnapshot?.Orientation ?? PlacementOrientation.Rot0;
        public Vector2Int BoundingSize => TargetPlacementSnapshot?.BoundingSize ?? Vector2Int.one;
    }

    public sealed class TransferExecutionSummary
    {
        public TransferExecutionSummary(
            bool success,
            int succeededEntries,
            int failedEntries,
            int transferredAmount,
            bool isPartial,
            DropResult dropResult,
            IReadOnlyList<ExecutedTransferEntry> executedEntries = null)
        {
            Success = success;
            SucceededEntries = succeededEntries;
            FailedEntries = failedEntries;
            TransferredAmount = transferredAmount;
            IsPartial = isPartial;
            DropResult = dropResult;
            ExecutedEntries = executedEntries ?? System.Array.Empty<ExecutedTransferEntry>();
        }

        public bool Success { get; }
        public int SucceededEntries { get; }
        public int FailedEntries { get; }
        public int TransferredAmount { get; }
        public bool IsPartial { get; }
        public DropResult DropResult { get; }
        public IReadOnlyList<ExecutedTransferEntry> ExecutedEntries { get; }
    }

    public sealed class TransferExecutionOptions
    {
        public GlobalRuleValidator GlobalRules { get; set; }
        public System.Func<InventorySwapContext, bool> SwapAttempting { get; set; }
        public System.Action<InventorySwapContext> SwapCompleted { get; set; }
    }

    /// <summary>
    /// Executes a prepared TransferPlan.
    /// Supports atomic mode (with rollback) and BestEffort.
    /// </summary>
    public class TransferPlanExecutor
    {
        private readonly struct DomainValidationResult
        {
            public DomainValidationResult(bool isValid, string failureReason)
            {
                IsValid = isValid;
                FailureReason = failureReason;
            }

            public bool IsValid { get; }
            public string FailureReason { get; }
        }

        private readonly struct SwapExecutionAttempt
        {
            public SwapExecutionAttempt(bool success, PendingSwapOutcome outcome, string failureReason)
            {
                Success = success;
                Outcome = outcome;
                FailureReason = failureReason;
            }

            public bool Success { get; }
            public PendingSwapOutcome Outcome { get; }
            public string FailureReason { get; }
        }

        public TransferExecutionSummary Execute(TransferPlan plan, TransferExecutionOptions options = null)
            => ExecuteCoreAsync(plan, options, allowAsyncDomainValidation: false, CancellationToken.None).GetAwaiter().GetResult();

        public Task<TransferExecutionSummary> ExecuteAsync(
            TransferPlan plan,
            TransferExecutionOptions options = null,
            CancellationToken cancellationToken = default)
            => ExecuteCoreAsync(plan, options, allowAsyncDomainValidation: true, cancellationToken);

        private async Task<TransferExecutionSummary> ExecuteCoreAsync(
            TransferPlan plan,
            TransferExecutionOptions options,
            bool allowAsyncDomainValidation,
            CancellationToken cancellationToken)
        {
            if (plan == null || !plan.IsValid || plan.Entries == null || plan.Entries.Count == 0)
            {
                var failed = DropResult.Failed("Invalid transfer plan");
                return new TransferExecutionSummary(false, 0, 0, 0, false, failed);
            }

            bool atomic = plan.Policy.BatchMode == BatchMode.Atomic;
            var snapshots = atomic ? CaptureSnapshots(plan) : null;

            if (atomic && snapshots == null)
            {
                var failed = DropResult.FailedBatch("Atomic execution requires snapshot-capable inventories", 0, plan.Entries.Count);
                return new TransferExecutionSummary(false, 0, plan.Entries.Count, 0, false, failed);
            }

            int succeededEntries = 0;
            int failedEntries = 0;
            int transferredAmount = 0;
            IItemAdapter lastItemAdapter = null;
            BaseSlot lastTargetBaseSlot = null;
            PlacementSnapshot lastTargetPlacementSnapshot = null;
            bool hadPartialTransfer = false;
            var successfulOutcomes = new List<InventoryTransferResult>(plan.Entries.Count);
            var successfulDomainContexts = new List<TransferDomainContext>(plan.Entries.Count);
            var successfulSwaps = new List<PendingSwapOutcome>(plan.Entries.Count);
            var executedEntries = new List<ExecutedTransferEntry>(plan.Entries.Count);

            foreach (var plannedEntry in plan.Entries)
            {
                if (plannedEntry == null || !plannedEntry.IsPlanned)
                {
                    failedEntries++;
                    if (atomic)
                    {
                        RestoreSnapshots(snapshots);
                        return BuildFailedSummary(
                            "Atomic execution failed: plan contains non-plannable entry",
                            succeededEntries,
                            failedEntries,
                            transferredAmount);
                    }
                    continue;
                }

                int entryTransferred = 0;
                bool entryFailed = false;

                if (plannedEntry.RequiresOccupiedHandler)
                {
                    if (plan.TargetInventory is UniversalInventory occupiedUni
                        && occupiedUni.ExecuteOccupiedSlotDrop(plannedEntry.Entry, plannedEntry.OccupiedTargetBaseSlot))
                    {
                        succeededEntries++;
                        entryTransferred = plannedEntry.RequestedAmount;
                        transferredAmount += entryTransferred;
                        lastItemAdapter = plannedEntry.Entry.Stack?.PrimaryAdapter;
                        lastTargetBaseSlot = plannedEntry.OccupiedTargetBaseSlot;
                        executedEntries.Add(new ExecutedTransferEntry(
                            plannedEntry.Entry.SourceBaseSlot,
                            plannedEntry.OccupiedTargetBaseSlot,
                            plannedEntry.Entry.Stack?.PrimaryAdapter,
                            entryTransferred));
                        Extensions.DragAndDropLog($"<color=green>[TransferPlanExecutor] OccupiedHandler succeeded</color>");
                    }
                    else
                    {
                        entryFailed = true;
                        Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] OccupiedHandler failed</color>");
                    }
                }
                else if (plannedEntry.RequiresSwap)
                {
                    var swapAttempt = await TryExecuteSwapCoreAsync(
                        plannedEntry, plan.TargetInventory, options,
                        allowAsyncDomainValidation, cancellationToken);

                    if (swapAttempt.Success)
                    {
                        var swapOutcome = swapAttempt.Outcome;
                        foreach (var domainContext in swapOutcome.DomainContexts)
                            successfulDomainContexts.Add(domainContext);
                        successfulSwaps.Add(swapOutcome);
                        succeededEntries++;
                        entryTransferred = swapOutcome.MovedAmount;
                        transferredAmount += swapOutcome.MovedAmount;
                        lastItemAdapter = swapOutcome.SwapResult.SourceStackBefore?.PrimaryAdapter;
                        lastTargetBaseSlot = swapOutcome.TargetBaseSlot;
                        if (swapOutcome.MovedAmount > 0 && swapOutcome.SwapResult.SourceStackBefore?.PrimaryAdapter != null)
                        {
                            executedEntries.Add(new ExecutedTransferEntry(
                                swapOutcome.SourceBaseSlot,
                                swapOutcome.TargetBaseSlot,
                                swapOutcome.SwapResult.SourceStackBefore.PrimaryAdapter,
                                swapOutcome.MovedAmount));
                        }
                    }
                    else
                    {
                        entryFailed = true;
                        Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Swap failed: {swapAttempt.FailureReason}</color>");
                    }
                }
                else if (plannedEntry.HasPlacementAllocation)
                {
                    var allocation = plannedEntry.PlacementAllocation.Value;
                    var targetPlacementInventory = plan.TargetInventory as IPlacementInventory;
                    var anchorSlot = targetPlacementInventory?.GetSlot(allocation.AnchorIndex);

                    if (targetPlacementInventory == null || anchorSlot == null || allocation.Amount <= 0)
                    {
                        entryFailed = true;
                    }
                    else if (!ItemStack.TryCreate(
                                 plannedEntry.Entry.Stack.Adapters.Take(allocation.Amount),
                                 out var requestStack))
                    {
                        entryFailed = true;
                    }
                    else
                    {
                        var request = new InventoryTransferRequest(
                            plannedEntry.Entry.SourceInventory,
                            plannedEntry.Entry.SourceBaseSlot,
                            plan.TargetInventory,
                            anchorSlot,
                            requestStack,
                            allocation.Orientation);

                        if (!TryBuildDomainContext(request, plannedEntry.PreviewTargetItemAdapter, out var domainContext))
                        {
                            var domainFailure = "Failed to build domain context";
                            Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain validation failed: {domainFailure}</color>");
                            entryFailed = true;
                        }
                        else
                        {
                            var validationResult = await ValidateDomainHandlersAsync(domainContext, allowAsyncDomainValidation, cancellationToken);
                            if (!validationResult.IsValid)
                            {
                                Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain validation failed: {validationResult.FailureReason}</color>");
                                entryFailed = true;
                            }
                            else if (!TryExecuteTransfer(request, out var outcome, allocation) || outcome.Amount <= 0)
                            {
                                entryFailed = true;
                            }
                            else
                            {
                                entryTransferred += outcome.Amount;
                                transferredAmount += outcome.Amount;
                                lastItemAdapter = outcome.ItemAdapter;
                                lastTargetBaseSlot = outcome.TargetBaseSlot ?? anchorSlot;
                                lastTargetPlacementSnapshot = outcome.TargetPlacementSnapshot;
                                hadPartialTransfer |= outcome.IsPartialTransfer;
                                domainContext.MarkCommitted(outcome);
                                successfulOutcomes.Add(outcome);
                                successfulDomainContexts.Add(domainContext);
                                executedEntries.Add(new ExecutedTransferEntry(
                                    outcome.SourceBaseSlot,
                                    outcome.TargetBaseSlot ?? anchorSlot,
                                    outcome.ItemAdapter,
                                    outcome.Amount,
                                    outcome.TargetPlacementSnapshot,
                                    outcome.SourcePlacementSnapshot));
                            }
                        }
                    }
                }
                else
                {
                    int adapterOffset = 0;
                    foreach (var allocation in plannedEntry.Allocations)
                    {
                        if (allocation.Amount <= 0)
                        {
                            entryFailed = true;
                            break;
                        }

                        if (!ItemStack.TryCreate(
                                plannedEntry.Entry.Stack.Adapters.Skip(adapterOffset).Take(allocation.Amount),
                                out var requestStack))
                        {
                            entryFailed = true;
                            break;
                        }

                        adapterOffset += allocation.Amount;

                        var request = new InventoryTransferRequest(
                            plannedEntry.Entry.SourceInventory,
                            plannedEntry.Entry.SourceBaseSlot,
                            plan.TargetInventory,
                            allocation.BaseSlot,
                            requestStack,
                            plannedEntry.Entry.Orientation);

                        if (!TryBuildDomainContext(request, plannedEntry.PreviewTargetItemAdapter, out var domainContext))
                        {
                            var domainFailure = "Failed to build domain context";
                            Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain validation failed: {domainFailure}</color>");
                            entryFailed = true;
                            break;
                        }

                        var validationResult = await ValidateDomainHandlersAsync(domainContext, allowAsyncDomainValidation, cancellationToken);
                        if (!validationResult.IsValid)
                        {
                            Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain validation failed: {validationResult.FailureReason}</color>");
                            entryFailed = true;
                            break;
                        }

                        if (!TryExecuteTransfer(request, out var outcome) || outcome.Amount <= 0)
                        {
                            entryFailed = true;
                            break;
                        }

                        entryTransferred += outcome.Amount;
                        transferredAmount += outcome.Amount;
                        lastItemAdapter = outcome.ItemAdapter;
                        lastTargetBaseSlot = outcome.TargetBaseSlot ?? allocation.BaseSlot;
                        lastTargetPlacementSnapshot = outcome.TargetPlacementSnapshot;
                        hadPartialTransfer |= outcome.IsPartialTransfer;
                        domainContext.MarkCommitted(outcome);
                        successfulOutcomes.Add(outcome);
                        successfulDomainContexts.Add(domainContext);
                        executedEntries.Add(new ExecutedTransferEntry(
                            outcome.SourceBaseSlot,
                            outcome.TargetBaseSlot ?? allocation.BaseSlot,
                            outcome.ItemAdapter,
                            outcome.Amount,
                            outcome.TargetPlacementSnapshot,
                            outcome.SourcePlacementSnapshot));
                    }
                }

                if (entryFailed)
                {
                    failedEntries++;
                    if (atomic)
                    {
                        RestoreSnapshots(snapshots);
                        return BuildFailedSummary(
                            "Atomic execution failed while applying plan",
                            succeededEntries,
                            failedEntries,
                            transferredAmount);
                    }
                }
                else if (entryTransferred > 0)
                {
                    if (!plannedEntry.RequiresSwap)
                    {
                        succeededEntries++;
                    }
                    if (entryTransferred < plannedEntry.RequestedAmount)
                    {
                        hadPartialTransfer = true;
                    }
                }
                else
                {
                    failedEntries++;
                    if (atomic)
                    {
                        RestoreSnapshots(snapshots);
                        return BuildFailedSummary(
                            "Atomic execution failed: no items transferred for planned entry",
                            succeededEntries,
                            failedEntries,
                            transferredAmount);
                    }
                }
            }

            bool success = succeededEntries > 0 && (failedEntries == 0 || plan.Policy.BatchMode == BatchMode.BestEffort);
            bool isPartial = hadPartialTransfer || failedEntries > 0 || (succeededEntries > 0 && failedEntries > 0);

            if (!success)
            {
                var failed = DropResult.FailedBatch(
                    "No planned entries were executed",
                    succeededEntries,
                    failedEntries);
                return new TransferExecutionSummary(false, succeededEntries, failedEntries, transferredAmount, isPartial, failed);
            }

            var result = DropResult.SucceededBatch(
                itemAdapter: lastItemAdapter,
                amount: transferredAmount,
                targetBaseSlot: lastTargetBaseSlot,
                targetInventory: plan.TargetInventory,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries,
                isPartialTransfer: isPartial,
                remainingInSource: 0,
                placementSnapshot: lastTargetPlacementSnapshot);

            // Emit events only after the whole operation completes successfully.
            // In Atomic mode this prevents false-positive events before a later rollback.
            DispatchDomainSuccessHooks(successfulDomainContexts);
            DispatchTransferEvents(successfulOutcomes);
            DispatchSwapEvents(successfulSwaps, options);

            Extensions.DragAndDropLog($"<color=green>[TransferPlanExecutor] Executed plan: successEntries={succeededEntries}, failedEntries={failedEntries}, amount={transferredAmount}</color>");
            return new TransferExecutionSummary(
                success,
                succeededEntries,
                failedEntries,
                transferredAmount,
                isPartial,
                result,
                executedEntries);
        }

        private static bool TryBuildDomainContext(InventoryTransferRequest request, IItemAdapter previewTargetItemAdapter, out TransferDomainContext context)
        {
            context = null;

            if (previewTargetItemAdapter == null)
                return false;

            context = new TransferDomainContext(
                request.SourceInventory,
                request.TargetInventory,
                request.SourceBaseSlot,
                request.TargetBaseSlot,
                request.DraggedStack.PrimaryAdapter,
                previewTargetItemAdapter,
                request.DraggedStack.Count,
                DetermineTransferKind(request));
            return true;
        }

        private static async Task<DomainValidationResult> ValidateDomainHandlersAsync(
            TransferDomainContext context,
            bool allowAsyncDomainValidation,
            CancellationToken cancellationToken)
        {
            foreach (var handler in EnumerateDomainHandlers(context))
            {
                try
                {
                    var syncResult = handler.CanCommitTransfer(context);
                    if (!syncResult.IsValid)
                    {
                        return new DomainValidationResult(
                            false,
                            string.IsNullOrEmpty(syncResult.FailureReason)
                                ? "Domain validation failed"
                                : syncResult.FailureReason);
                    }

                    if (!allowAsyncDomainValidation || handler is not IAsyncTransferDomainHandler asyncHandler)
                        continue;

                    cancellationToken.ThrowIfCancellationRequested();
                    var asyncResult = await asyncHandler.CanCommitTransferAsync(context, cancellationToken);
                    if (!asyncResult.IsValid)
                    {
                        return new DomainValidationResult(
                            false,
                            string.IsNullOrEmpty(asyncResult.FailureReason)
                                ? "Async domain validation failed"
                                : asyncResult.FailureReason);
                    }
                }
                catch (OperationCanceledException)
                {
                    return new DomainValidationResult(false, "Transfer validation was cancelled");
                }
                catch (System.Exception ex)
                {
                    return new DomainValidationResult(false, ex.Message);
                }
            }

            return new DomainValidationResult(true, null);
        }

        private static void DispatchDomainSuccessHooks(IReadOnlyList<TransferDomainContext> contexts)
        {
            foreach (var context in contexts)
            {
                foreach (var handler in EnumerateDomainHandlers(context))
                {
                    try
                    {
                        handler.OnTransferSucceeded(context);
                    }
                    catch (System.Exception ex)
                    {
                        Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain success hook threw: {ex.Message}</color>");
                    }
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<ITransferDomainHandler> EnumerateDomainHandlers(TransferDomainContext context)
        {
            var sourceHandler = context.SourceInventory?.DataBinding as ITransferDomainHandler;
            if (sourceHandler != null)
            {
                yield return sourceHandler;
            }

            var targetHandler = context.TargetInventory?.DataBinding as ITransferDomainHandler;
            if (targetHandler != null && targetHandler != sourceHandler)
            {
                yield return targetHandler;
            }
        }

        private async Task<SwapExecutionAttempt> TryExecuteSwapCoreAsync(
            PlannedEntryTransfer plannedEntry,
            IInventory fallbackTargetInventory,
            TransferExecutionOptions options,
            bool allowAsyncDomainValidation,
            CancellationToken cancellationToken)
        {
            var swapData = plannedEntry.SwapData;
            if (swapData == null)
                return new SwapExecutionAttempt(false, default, "Missing swap data in plan");

            var sourceSlot = plannedEntry.Entry.SourceBaseSlot;
            var sourceInventory = plannedEntry.Entry.SourceInventory;
            var targetSlot = plannedEntry.SwapTargetBaseSlot;
            var targetInventory = targetSlot?.Inventory ?? fallbackTargetInventory;

            if (sourceSlot == null || targetSlot == null || sourceInventory == null || targetInventory == null)
                return new SwapExecutionAttempt(false, default, "Invalid swap target/source");

            if (sourceSlot.IsEmpty || targetSlot.IsEmpty)
                return new SwapExecutionAttempt(false, default, "Swap requires non-empty source and target slots");

            var swapContext = new InventorySwapContext(
                swapData.SourceStackBefore,
                swapData.TargetStackBefore,
                sourceSlot,
                targetSlot,
                sourceInventory,
                targetInventory);

            var swapDomainContexts = BuildSwapDomainContexts(
                sourceInventory,
                targetInventory,
                sourceSlot,
                targetSlot,
                swapData.SourceStackBefore,
                swapData.TargetStackBefore,
                swapData.TargetStackAfter,
                swapData.SourceStackAfter);

            var forwardValidation = await ValidateDomainHandlersAsync(swapDomainContexts[0], allowAsyncDomainValidation, cancellationToken);
            if (!forwardValidation.IsValid)
                return new SwapExecutionAttempt(false, default, forwardValidation.FailureReason);

            var reverseValidation = await ValidateDomainHandlersAsync(swapDomainContexts[1], allowAsyncDomainValidation, cancellationToken);
            if (!reverseValidation.IsValid)
                return new SwapExecutionAttempt(false, default, reverseValidation.FailureReason);

            if (options?.SwapAttempting != null && !options.SwapAttempting(swapContext))
                return new SwapExecutionAttempt(false, default, "Swap cancelled by listener");

            if (!TryCommitSwapViaPlacement(
                    sourceSlot, targetSlot,
                    sourceInventory, targetInventory,
                    swapData,
                    out var swapResult,
                    out var commitFailure))
            {
                return new SwapExecutionAttempt(false, default, commitFailure);
            }

            swapDomainContexts[0].MarkCommitted(targetSlot, swapData.TargetStackAfter.PrimaryAdapter, swapData.TargetStackAfter.Count);
            swapDomainContexts[1].MarkCommitted(sourceSlot, swapData.SourceStackAfter.PrimaryAdapter, swapData.SourceStackAfter.Count);

            var targetUniversal = targetInventory as UniversalInventory;
            var sourceUniversal = sourceInventory as UniversalInventory;
            var swapOutcome = new PendingSwapOutcome(
                swapContext,
                targetUniversal,
                sourceUniversal,
                targetSlot,
                sourceSlot,
                swapResult,
                swapDomainContexts);
            return new SwapExecutionAttempt(true, swapOutcome, null);
        }

        private static TransferDomainContext[] BuildSwapDomainContexts(
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            ItemStack sourceStackBefore,
            ItemStack targetStackBefore,
            ItemStack targetStackAfter,
            ItemStack sourceStackAfter)
        {
            var forward = new TransferDomainContext(
                sourceInventory,
                targetInventory,
                sourceBaseSlot,
                targetBaseSlot,
                sourceStackBefore.PrimaryAdapter,
                targetStackAfter.PrimaryAdapter,
                sourceStackBefore.Count,
                TransferKind.Swap);
            var reverse = new TransferDomainContext(
                targetInventory,
                sourceInventory,
                targetBaseSlot,
                sourceBaseSlot,
                targetStackBefore.PrimaryAdapter,
                sourceStackAfter.PrimaryAdapter,
                targetStackBefore.Count,
                TransferKind.Swap);

            forward.CounterpartContext = reverse;
            reverse.CounterpartContext = forward;

            return new[] { forward, reverse };
        }

        private static bool TryCommitSwapViaPlacement(
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory,
            IInventory targetInventory,
            PlannedSwapData swapData,
            out SwapOperationResult result,
            out string failureReason)
        {
            result = default;
            failureReason = null;

            if (sourceBaseSlot == null || targetBaseSlot == null)
            {
                failureReason = "Invalid swap slots";
                return false;
            }

            // Capture snapshots for rollback
            var sourceSnapshotProvider = sourceInventory as IInventorySnapshotProvider;
            var targetSnapshotProvider = targetInventory as IInventorySnapshotProvider;
            var sourceSnapshot = sourceSnapshotProvider?.CaptureSnapshot();
            var targetSnapshot = targetSnapshotProvider?.CaptureSnapshot();

            try
            {
                // Clear both slots
                sourceBaseSlot.Clear();
                targetBaseSlot.Clear();

                // Clone converted stacks for placement (placement consumes items from the stack)
                var forwardStack = CloneStack(swapData.TargetStackAfter);
                var reverseStack = CloneStack(swapData.SourceStackAfter);

                // Forward: place source items into target inventory at target slot
                targetInventory.TryAddToSlot(forwardStack, targetBaseSlot, sourceInventory, sourceBaseSlot.Index);
                if (!forwardStack.IsEmpty)
                    targetInventory.TryAddStack(forwardStack, -1);

                if (!forwardStack.IsEmpty)
                {
                    failureReason = "Failed to place source items into target inventory";
                    RestoreSwapSnapshots(sourceInventory, sourceSnapshotProvider, sourceSnapshot,
                        targetInventory, targetSnapshotProvider, targetSnapshot);
                    return false;
                }

                // Reverse: place target items into source inventory at source slot (+ distribute)
                sourceInventory.TryAddToSlot(reverseStack, sourceBaseSlot, targetInventory, targetBaseSlot.Index);
                if (!reverseStack.IsEmpty)
                    sourceInventory.TryAddStack(reverseStack, -1);

                if (!reverseStack.IsEmpty)
                {
                    failureReason = "Failed to place target items into source inventory";
                    RestoreSwapSnapshots(sourceInventory, sourceSnapshotProvider, sourceSnapshot,
                        targetInventory, targetSnapshotProvider, targetSnapshot);
                    return false;
                }

                sourceInventory.UpdateAllVisuals();
                targetInventory.UpdateAllVisuals();

                result = new SwapOperationResult(
                    CloneStack(swapData.TargetStackBefore),
                    CloneStack(swapData.SourceStackBefore),
                    CloneStack(swapData.TargetStackAfter),
                    CloneStack(swapData.SourceStackAfter));
                return true;
            }
            catch (Exception ex)
            {
                RestoreSwapSnapshots(sourceInventory, sourceSnapshotProvider, sourceSnapshot,
                    targetInventory, targetSnapshotProvider, targetSnapshot);
                failureReason = ex.Message;
                return false;
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
            if (sourceProvider != null && sourceSnapshot != null)
            {
                sourceProvider.RestoreSnapshot(sourceSnapshot);
                sourceInventory.UpdateAllVisuals();
            }

            if (targetProvider != null && targetSnapshot != null)
            {
                targetProvider.RestoreSnapshot(targetSnapshot);
                targetInventory.UpdateAllVisuals();
            }
        }

        private static TransferKind DetermineTransferKind(InventoryTransferRequest request)
        {
            if (request.TargetBaseSlot != null && !request.TargetBaseSlot.IsEmpty)
                return TransferKind.Merge;

            if (request.SourceBaseSlot?.Stack != null && request.SourceBaseSlot.Stack.Count > request.DraggedStack.Count)
                return TransferKind.Split;

            return TransferKind.Move;
        }

        private bool TryExecuteTransfer(
            InventoryTransferRequest request,
            out InventoryTransferResult result,
            PlannedPlacementAllocation? placementAllocation = null)
        {
            result = default;

            if (!request.IsValid)
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Invalid transfer request</color>");
                return false;
            }

            var sourceInventory = request.SourceInventory;
            var targetInventory = request.TargetInventory;
            var sourceSlot = request.SourceBaseSlot;
            var targetSlot = request.TargetBaseSlot;
            var draggedStack = request.DraggedStack;

            var sourceSnapshotProvider = sourceInventory as IInventorySnapshotProvider;
            var targetSnapshotProvider = targetInventory as IInventorySnapshotProvider;
            var sourceStackStore = sourceInventory as ISlotStackStore
                ?? sourceSlot?.Inventory as ISlotStackStore;

            var sourceInventorySnapshot = sourceSnapshotProvider?.CaptureSnapshot();
            var targetInventorySnapshot = targetSnapshotProvider?.CaptureSnapshot();
            var sourcePlacementSnapshot = ResolvePlacementSnapshot(sourceInventory, sourceSlot);

            int requestedAmount = draggedStack.Count;
            int transferAmount = requestedAmount;

            Extensions.DragAndDropLog($"<color=cyan>[TransferPlanExecutor] Requested: {requestedAmount}, Transfer: {transferAmount}</color>");

            if (sourceStackStore == null ||
                !sourceStackStore.TrySplitFromSlot(sourceSlot, transferAmount, out var transferStack) ||
                transferStack.IsEmpty)
                return false;

            transferAmount = transferStack.Count;
            sourceSlot.UpdateVisuals();
            if (sourceInventory is IPlacementInventory sourcePlacementInventory && sourcePlacementInventory.Grid.HasValue)
                sourceInventory.UpdateAllVisuals();

            var sourceRemovedStack = transferStack.CreateCopy();

            if (!TransferItemConversionUtility.TryConvertOutgoingStack(sourceInventory, transferStack))
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Source outgoing conversion failed, rolling back</color>");
                if (!sourceStackStore.TryAddToSlotStack(sourceSlot, transferStack))
                    InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot);
                return false;
            }

            if (!TransferItemConversionUtility.TryConvertIncomingStack(targetInventory, transferStack))
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Target incoming conversion failed, rolling back</color>");
                if (!sourceStackStore.TryAddToSlotStack(sourceSlot, sourceRemovedStack))
                    InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot);
                return false;
            }

            var convertedTransferCopy = transferStack.CreateCopy();

            var operationContext = new SlotOperationContext();
            var placementOperation = new TargetPlacementOperation(
                targetInventory,
                targetSlot,
                sourceInventory,
                sourceSlot,
                transferStack,
                transferAmount,
                request.Orientation,
                targetInventorySnapshot,
                operationContext,
                placementAllocation);

            bool added = TryAddToTargetInventory(placementOperation);
            if (!added)
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Failed to add to target, rolling back</color>");
                InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot);
                InventorySnapshotUtility.RestoreInventorySnapshot(targetInventory, targetSnapshotProvider, targetInventorySnapshot);
                return false;
            }

            int actuallyAdded = transferAmount - (transferStack?.Count ?? 0);
            int actualRemaining = requestedAmount - actuallyAdded;

            if (!transferStack.IsEmpty)
            {
                Extensions.DragAndDropLog($"<color=yellow>[TransferPlanExecutor] {transferStack.Count} items not placed, returning to source</color>");
                if (sourceSlot.IsEmpty)
                {
                    if (!sourceStackStore.TrySetStackForSlot(sourceSlot, transferStack))
                    {
                        Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Failed to restore unplaced items to empty source, rolling back</color>");
                        InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot);
                        InventorySnapshotUtility.RestoreInventorySnapshot(targetInventory, targetSnapshotProvider, targetInventorySnapshot);
                        return false;
                    }
                }
                else
                {
                    if (!sourceStackStore.TryAddToSlotStack(sourceSlot, transferStack))
                    {
                        Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Failed to return unplaced items to source, rolling back</color>");
                        InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot);
                        InventorySnapshotUtility.RestoreInventorySnapshot(targetInventory, targetSnapshotProvider, targetInventorySnapshot);
                        return false;
                    }
                }

                sourceSlot.UpdateVisuals();
            }

            var resolvedSlot = operationContext.ResolvedBaseSlot ?? targetSlot;
            bool targetWasEmpty = resolvedSlot != null && operationContext.TargetWasEmptyBefore;

            if (resolvedSlot == null && targetInventorySnapshot != null &&
                InventorySnapshotUtility.TryResolveSlotChange(targetInventory, targetInventorySnapshot, out var changedSlot, out var wasEmptyBefore))
            {
                resolvedSlot = changedSlot;
                targetWasEmpty = wasEmptyBefore;
            }

            // Capture the adapters that were actually transferred.
            // For single-slot placement (Stackable), take them from the resolved slot (adapters were appended at the end).
            // For multi-slot distribution (Unique), the resolved slot contains only 1 item,
            // so use the saved copy of the converted stack instead.
            ItemStack transferredStack;
            if (resolvedSlot?.Stack != null && actuallyAdded > 0 && actuallyAdded <= resolvedSlot.Stack.Count)
                transferredStack = resolvedSlot.Stack.CreateCopy(actuallyAdded);
            else if (actuallyAdded > 0 && convertedTransferCopy != null && !convertedTransferCopy.IsEmpty)
                transferredStack = convertedTransferCopy.CreateCopy(actuallyAdded);
            else
                transferredStack = ItemStack.Empty();

            var targetPlacementSnapshot = ResolvePlacementSnapshot(targetInventory, resolvedSlot);
            result = new InventoryTransferResult(
                sourceInventory,
                targetInventory,
                sourceSlot,
                resolvedSlot,
                sourceRemovedStack,
                transferredStack,
                targetWasEmpty,
                actualRemaining,
                targetPlacementSnapshot,
                sourcePlacementSnapshot);

            Extensions.DragAndDropLog($"<color=green>[TransferPlanExecutor] Transfer complete: {actuallyAdded} transferred, {actualRemaining} remaining in source</color>");
            return true;
        }

        private bool TryAddToTargetInventory(TargetPlacementOperation operation)
        {
            if (operation.TargetInventory == null)
                return false;

            operation.OperationContext?.ResetResult();

            if (TryAddToTargetPlacement(operation))
                return true;

            if (operation.RequiresStrategyPlacement)
            {
                Extensions.DragAndDropLog($"<color=cyan>[TransferPlanExecutor] Using strategy placement mode ({operation.TransferStack.Count} items)</color>");

                operation.TargetInventory.TryAddStack(operation.TransferStack, -1);
                int added = operation.TransferAmount - operation.TransferStack.Count;
                if (added > 0)
                {
                    if (operation.OperationContext != null &&
                        InventorySnapshotUtility.TryResolveSlotChange(operation.TargetInventory, operation.TargetSnapshot, out var slot, out var wasEmptyBefore))
                    {
                        operation.OperationContext.RecordResult(slot, wasEmptyBefore, added);
                    }

                    return true;
                }

                return false;
            }

            if (operation.RequestedBaseSlot != null)
            {
                bool wasEmpty = operation.RequestedBaseSlot.IsEmpty;
                if (operation.TargetInventory.TryAddToSlot(
                        operation.TransferStack,
                        operation.RequestedBaseSlot,
                        operation.SourceInventory,
                        operation.SourceBaseSlot.Index,
                        operation.OperationContext))
                {
                    if (operation.OperationContext?.ResolvedBaseSlot == null)
                    {
                        operation.OperationContext.RecordResult(operation.RequestedBaseSlot, wasEmpty, operation.TransferAmount);
                    }

                    return operation.TransferStack.IsEmpty;
                }
            }
            else if (operation.TargetInventory.TryAddStack(operation.TransferStack, -1))
            {
                int added = operation.TransferAmount - operation.TransferStack.Count;
                if (added > 0)
                {
                    if (operation.OperationContext != null &&
                        InventorySnapshotUtility.TryResolveSlotChange(operation.TargetInventory, operation.TargetSnapshot, out var slot, out var wasEmptyBefore))
                    {
                        operation.OperationContext.RecordResult(slot, wasEmptyBefore, added);
                    }

                    return true;
                }
            }

            return false;
        }

        private bool TryAddToTargetPlacement(TargetPlacementOperation operation)
        {
            if (operation.TargetInventory is not IPlacementInventory targetPlacementInventory ||
                operation.TransferStack == null ||
                operation.TransferStack.IsEmpty)
                return false;

            var strategy = targetPlacementInventory.PlacementStrategy;
            if (strategy == null)
                return false;

            var allocation = operation.PlacementAllocation
                ?? new PlannedPlacementAllocation(
                    operation.RequestedBaseSlot?.Index ?? -1,
                    operation.Orientation,
                    PlacementShapeUtility.Resolve(operation.TransferStack.PrimaryAdapter),
                    operation.TransferAmount);

            var executionContext = new ShapedPlacementExecutionContext(
                operation.TargetInventory,
                operation.TransferStack,
                operation.TransferAmount,
                allocation,
                operation.Orientation,
                operation.OperationContext);

            var result = strategy.TryExecuteShapedPlacement(executionContext);
            switch (result.Outcome)
            {
                case ShapedPlacementExecutionOutcome.Placed:
                    operation.OperationContext?.RecordResult(
                        result.ResolvedAnchorSlot,
                        result.TargetWasEmpty,
                        result.PlacedAmount);
                    return operation.TransferStack.IsEmpty;

                case ShapedPlacementExecutionOutcome.Failed:
                    Extensions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Shaped placement failed: {result.FailureReason}</color>");
                    return false;

                case ShapedPlacementExecutionOutcome.NotApplicable:
                default:
                    return false;
            }
        }

        private static PlacementSnapshot ResolvePlacementSnapshot(
            IInventory inventory,
            BaseSlot resolvedSlot)
        {
            if (inventory is IPlacementInventory placementInventory && resolvedSlot != null)
            {
                var placement = placementInventory.GetPlacementAt(resolvedSlot);
                if (placement != null)
                    return PlacementSnapshot.FromPlacement(placement, placementInventory.GetSlot);
            }

            return resolvedSlot != null
                ? new PlacementSnapshot(
                    resolvedSlot.Index,
                    PlacementOrientation.Rot0,
                    Vector2Int.one,
                    new[] { resolvedSlot.Index },
                    resolvedSlot,
                    new[] { resolvedSlot },
                    coveredOffsets: new[] { Vector2Int.zero })
                : null;
        }

        private static void DispatchTransferEvents(IReadOnlyList<InventoryTransferResult> outcomes)
        {
            if (outcomes == null)
                return;

            foreach (var outcome in outcomes)
            {
                if (outcome.SourceItemAdapter == null || outcome.TargetItemAdapter == null || outcome.Amount <= 0)
                    continue;

                if (outcome.SourceInventory is UniversalInventory sourceUniversal)
                {
                    sourceUniversal.EmitItemRemoved(
                        outcome.SourceRemovedStack,
                        outcome.SourceBaseSlot?.Index ?? -1,
                        outcome.TargetInventory,
                        outcome.SourceBaseSlot,
                        outcome.TargetBaseSlot,
                        outcome.SourcePlacementSnapshot);
                    sourceUniversal.HandleSlotEmptied(outcome.SourceBaseSlot);
                }

                if (outcome.TargetInventory is UniversalInventory targetUniversal && outcome.TargetBaseSlot != null)
                {
                    targetUniversal.EmitItemAdded(
                        outcome.TransferredStack,
                        outcome.TargetBaseSlot.Index,
                        outcome.SourceInventory,
                        outcome.SourceBaseSlot,
                        outcome.TargetBaseSlot,
                        outcome.TargetPlacementSnapshot);
                }
            }
        }

        private static void DispatchSwapEvents(IReadOnlyList<PendingSwapOutcome> outcomes, TransferExecutionOptions options)
        {
            if (outcomes == null)
                return;

            foreach (var outcome in outcomes)
            {
                if (outcome.TargetInventory != null &&
                    outcome.SwapResult.TargetStackBefore != null &&
                    !outcome.SwapResult.TargetStackBefore.IsEmpty)
                {
                    outcome.TargetInventory.EmitItemRemoved(
                        outcome.SwapResult.TargetStackBefore,
                        outcome.TargetBaseSlot.Index,
                        outcome.SourceInventory,
                        outcome.TargetBaseSlot,
                        outcome.SourceBaseSlot);

                    outcome.TargetInventory.EmitItemAdded(
                        outcome.SwapResult.TargetStackAfter,
                        outcome.TargetBaseSlot.Index,
                        outcome.SourceInventory,
                        outcome.SourceBaseSlot,
                        outcome.TargetBaseSlot);
                }

                if (outcome.SourceInventory != null &&
                    outcome.SwapResult.SourceStackBefore != null &&
                    !outcome.SwapResult.SourceStackBefore.IsEmpty)
                {
                    outcome.SourceInventory.EmitItemRemoved(
                        outcome.SwapResult.SourceStackBefore,
                        outcome.SourceBaseSlot.Index,
                        outcome.TargetInventory,
                        outcome.SourceBaseSlot,
                        outcome.TargetBaseSlot);

                    outcome.SourceInventory.EmitItemAdded(
                        outcome.SwapResult.SourceStackAfter,
                        outcome.SourceBaseSlot.Index,
                        outcome.TargetInventory,
                        outcome.TargetBaseSlot,
                        outcome.SourceBaseSlot);
                }

                options?.SwapCompleted?.Invoke(outcome.SwapContext);
            }
        }

        private static ItemStack CloneStack(ItemStack stack)
        {
            return stack != null && ItemStack.TryCreate(stack.Adapters, out var copy)
                ? copy
                : ItemStack.Empty();
        }

        private static TransferExecutionSummary BuildFailedSummary(
            string reason,
            int succeededEntries,
            int failedEntries,
            int transferredAmount)
        {
            var failed = DropResult.FailedBatch(reason, succeededEntries, failedEntries);
            return new TransferExecutionSummary(false, succeededEntries, failedEntries, transferredAmount, true, failed);
        }

        private static Dictionary<IInventory, InventorySnapshot> CaptureSnapshots(TransferPlan plan)
        {
            var snapshots = new Dictionary<IInventory, InventorySnapshot>();

            foreach (var entry in plan.Entries)
            {
                if (entry?.Entry.SourceInventory == null)
                    continue;

                if (!CaptureSnapshot(entry.Entry.SourceInventory, snapshots))
                    return null;
            }

            if (!CaptureSnapshot(plan.TargetInventory, snapshots))
                return null;

            return snapshots;
        }

        private static bool CaptureSnapshot(IInventory inventory, Dictionary<IInventory, InventorySnapshot> snapshots)
        {
            if (inventory == null || snapshots.ContainsKey(inventory))
                return true;

            if (inventory is not IInventorySnapshotProvider provider)
                return false;

            snapshots[inventory] = provider.CaptureSnapshot();
            return true;
        }

        private static void RestoreSnapshots(Dictionary<IInventory, InventorySnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (var pair in snapshots)
            {
                if (pair.Key is IInventorySnapshotProvider provider)
                {
                    provider.RestoreSnapshot(pair.Value);
                    pair.Key.UpdateAllVisuals();
                }
            }
        }

        private readonly struct PendingSwapOutcome
        {
            public PendingSwapOutcome(
                InventorySwapContext swapContext,
                UniversalInventory targetInventory,
                UniversalInventory sourceInventory,
                BaseSlot targetBaseSlot,
                BaseSlot sourceBaseSlot,
                SwapOperationResult swapResult,
                IReadOnlyList<TransferDomainContext> domainContexts)
            {
                SwapContext = swapContext;
                TargetInventory = targetInventory;
                SourceInventory = sourceInventory;
                TargetBaseSlot = targetBaseSlot;
                SourceBaseSlot = sourceBaseSlot;
                SwapResult = swapResult;
                DomainContexts = domainContexts;
            }

            public InventorySwapContext SwapContext { get; }
            public UniversalInventory TargetInventory { get; }
            public UniversalInventory SourceInventory { get; }
            public BaseSlot TargetBaseSlot { get; }
            public BaseSlot SourceBaseSlot { get; }
            public SwapOperationResult SwapResult { get; }
            public IReadOnlyList<TransferDomainContext> DomainContexts { get; }
            public int MovedAmount => SwapResult.SourceStackBefore?.Count ?? 0;
        }
    }
}
