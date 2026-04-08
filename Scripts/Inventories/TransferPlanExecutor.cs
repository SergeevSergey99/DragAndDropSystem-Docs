using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;

namespace DragAndDropSystem.Inventories
{
    public readonly struct ExecutedTransferEntry
    {
        public ExecutedTransferEntry(BaseSlot sourceBaseSlot, BaseSlot targetBaseSlot, IItemAdapter itemAdapter, int amount)
        {
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            ItemAdapter = itemAdapter;
            Amount = amount;
        }

        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }
        public IItemAdapter ItemAdapter { get; }
        public int Amount { get; }
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
    /// Исполняет готовый TransferPlan.
    /// Поддерживает атомарный режим (с откатом) и BestEffort.
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
                            requestStack);

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
                        hadPartialTransfer |= outcome.IsPartialTransfer;
                        domainContext.MarkCommitted(outcome);
                        successfulOutcomes.Add(outcome);
                        successfulDomainContexts.Add(domainContext);
                        executedEntries.Add(new ExecutedTransferEntry(
                            outcome.SourceBaseSlot,
                            outcome.TargetBaseSlot ?? allocation.BaseSlot,
                            outcome.ItemAdapter,
                            outcome.Amount));
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
                remainingInSource: 0);

            // Эмитим события только после успешного завершения всей операции.
            // В Atomic это предотвращает "ложные" события при последующем откате.
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

        private bool TryExecuteTransfer(InventoryTransferRequest request, out InventoryTransferResult result)
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

            var sourceInventorySnapshot = sourceSnapshotProvider?.CaptureSnapshot();
            var targetInventorySnapshot = targetSnapshotProvider?.CaptureSnapshot();
            var sourceSlotState = InventorySnapshotUtility.CaptureSlotState(sourceSlot);

            int requestedAmount = draggedStack.Count;
            int transferAmount = requestedAmount;

            Extensions.DragAndDropLog($"<color=cyan>[TransferPlanExecutor] Requested: {requestedAmount}, Transfer: {transferAmount}</color>");

            var transferStack = sourceSlot.Stack.Split(transferAmount);
            if (transferStack.IsEmpty)
            {
                InventorySnapshotUtility.RestoreSlotState(sourceSlot, sourceSlotState);
                return false;
            }

            transferAmount = transferStack.Count;
            sourceSlot.UpdateVisuals();

            var sourceRemovedStack = transferStack.CreateCopy();

            if (!TransferItemConversionUtility.TryConvertOutgoingStack(sourceInventory, transferStack))
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Source outgoing conversion failed, rolling back</color>");
                sourceSlot.Stack.TryAddToStack(transferStack);
                sourceSlot.UpdateVisuals();
                return false;
            }

            if (!TransferItemConversionUtility.TryConvertIncomingStack(targetInventory, transferStack))
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Target incoming conversion failed, rolling back</color>");
                sourceSlot.Stack.TryAddToStack(sourceRemovedStack);
                sourceSlot.UpdateVisuals();
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
                targetInventorySnapshot,
                operationContext);

            bool added = TryAddToTargetInventory(placementOperation);
            if (!added)
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Failed to add to target, rolling back</color>");
                InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot, sourceSlot, sourceSlotState);
                InventorySnapshotUtility.RestoreInventorySnapshot(targetInventory, targetSnapshotProvider, targetInventorySnapshot, null, default);
                return false;
            }

            int actuallyAdded = transferAmount - (transferStack?.Count ?? 0);
            int actualRemaining = requestedAmount - actuallyAdded;

            if (!transferStack.IsEmpty)
            {
                Extensions.DragAndDropLog($"<color=yellow>[TransferPlanExecutor] {transferStack.Count} items not placed, returning to source</color>");
                if (sourceSlot.IsEmpty)
                {
                    sourceSlot.SetStack(transferStack);
                }
                else
                {
                    if (!sourceSlot.Stack.TryAddToStack(transferStack))
                    {
                        Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Failed to return unplaced items to source, rolling back</color>");
                        InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot, sourceSlot, sourceSlotState);
                        InventorySnapshotUtility.RestoreInventorySnapshot(targetInventory, targetSnapshotProvider, targetInventorySnapshot, null, default);
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

            // Захватываем реально перенесённые адаптеры.
            // Для single-slot placement (Stackable) берём из resolved slot (адаптеры добавлены в конец).
            // Для multi-slot distribution (Unique) resolved slot содержит только 1 предмет,
            // поэтому используем сохранённую копию сконвертированного стека.
            ItemStack transferredStack;
            if (resolvedSlot?.Stack != null && actuallyAdded > 0 && actuallyAdded <= resolvedSlot.Stack.Count)
                transferredStack = resolvedSlot.Stack.CreateCopy(actuallyAdded);
            else if (actuallyAdded > 0 && convertedTransferCopy != null && !convertedTransferCopy.IsEmpty)
                transferredStack = convertedTransferCopy.CreateCopy(actuallyAdded);
            else
                transferredStack = ItemStack.Empty();

            result = new InventoryTransferResult(
                sourceInventory,
                targetInventory,
                sourceSlot,
                resolvedSlot,
                sourceRemovedStack,
                transferredStack,
                targetWasEmpty,
                actualRemaining);

            Extensions.DragAndDropLog($"<color=green>[TransferPlanExecutor] Transfer complete: {actuallyAdded} transferred, {actualRemaining} remaining in source</color>");
            return true;
        }

        private bool TryAddToTargetInventory(TargetPlacementOperation operation)
        {
            if (operation.TargetInventory == null)
                return false;

            operation.OperationContext?.ResetResult();

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
                        outcome.TargetBaseSlot);
                    sourceUniversal.HandleSlotEmptied(outcome.SourceBaseSlot);
                }

                if (outcome.TargetInventory is UniversalInventory targetUniversal && outcome.TargetBaseSlot != null)
                {
                    targetUniversal.EmitItemAdded(
                        outcome.TransferredStack,
                        outcome.TargetBaseSlot.Index,
                        outcome.SourceInventory,
                        outcome.SourceBaseSlot,
                        outcome.TargetBaseSlot);
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
