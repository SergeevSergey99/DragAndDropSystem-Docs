using System;
using System.Collections.Generic;
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
        public ExecutedTransferEntry(ISlot sourceSlot, ISlot targetSlot, IInventoryItem item, int amount)
        {
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
            Item = item;
            Amount = amount;
        }

        public ISlot SourceSlot { get; }
        public ISlot TargetSlot { get; }
        public IInventoryItem Item { get; }
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

        private readonly RuleEvaluationService _ruleEvaluationService = new RuleEvaluationService();

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
            IInventoryItem lastItem = null;
            ISlot lastTargetSlot = null;
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

                if (plannedEntry.RequiresSwap)
                {
                    var swapAttempt = allowAsyncDomainValidation
                        ? await TryExecuteSwapAsync(plannedEntry, plan.TargetInventory, options, cancellationToken)
                        : TryExecuteSwap(plannedEntry, plan.TargetInventory, options);

                    if (swapAttempt.Success)
                    {
                        var swapOutcome = swapAttempt.Outcome;
                        foreach (var domainContext in swapOutcome.DomainContexts)
                            successfulDomainContexts.Add(domainContext);
                        successfulSwaps.Add(swapOutcome);
                        succeededEntries++;
                        entryTransferred = swapOutcome.MovedAmount;
                        transferredAmount += swapOutcome.MovedAmount;
                        lastItem = swapOutcome.SwapResult.SourceStackBefore?.Item;
                        lastTargetSlot = swapOutcome.TargetSlot;
                        if (swapOutcome.MovedAmount > 0 && swapOutcome.SwapResult.SourceStackBefore?.Item != null)
                        {
                            executedEntries.Add(new ExecutedTransferEntry(
                                swapOutcome.SourceSlot,
                                swapOutcome.TargetSlot,
                                swapOutcome.SwapResult.SourceStackBefore.Item,
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
                    foreach (var allocation in plannedEntry.Allocations)
                    {
                        if (allocation.Amount <= 0)
                        {
                            entryFailed = true;
                            break;
                        }

                        var request = new InventoryTransferRequest(
                            plannedEntry.Entry.SourceInventory,
                            plannedEntry.Entry.SourceSlot,
                            plan.TargetInventory,
                            allocation.Slot,
                            new ItemStack(plannedEntry.Entry.Stack.Item, allocation.Amount));

                        if (!TryBuildDomainContext(request, plannedEntry.PreviewTargetItem, out var domainContext))
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
                        lastItem = outcome.Item;
                        lastTargetSlot = outcome.TargetSlot ?? allocation.Slot;
                        hadPartialTransfer |= outcome.IsPartialTransfer;
                        domainContext.MarkCommitted(outcome);
                        successfulOutcomes.Add(outcome);
                        successfulDomainContexts.Add(domainContext);
                        executedEntries.Add(new ExecutedTransferEntry(
                            outcome.SourceSlot,
                            outcome.TargetSlot ?? allocation.Slot,
                            outcome.Item,
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
                item: lastItem,
                amount: transferredAmount,
                targetSlot: lastTargetSlot,
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

        private static bool TryBuildDomainContext(InventoryTransferRequest request, IInventoryItem previewTargetItem, out TransferDomainContext context)
        {
            context = null;

            var targetPreviewItem = previewTargetItem;
            if (targetPreviewItem == null &&
                !TransferItemConversionUtility.TryResolveTargetItem(
                    request.SourceInventory,
                    request.TargetInventory,
                    request.DraggedStack.Item,
                    out targetPreviewItem))
            {
                return false;
            }

            context = new TransferDomainContext(
                request.SourceInventory,
                request.TargetInventory,
                request.SourceSlot,
                request.TargetSlot,
                request.DraggedStack.Item,
                targetPreviewItem,
                request.DraggedStack.Count,
                DetermineTransferKind(request));
            return true;
        }

        private static bool ValidateDomainHandlers(TransferDomainContext context, out string failureReason)
        {
            failureReason = null;

            foreach (var handler in EnumerateDomainHandlers(context))
            {
                try
                {
                    var result = handler.CanCommitTransfer(context);
                    if (!result.IsValid)
                    {
                        failureReason = string.IsNullOrEmpty(result.FailureReason)
                            ? "Domain validation failed"
                            : result.FailureReason;
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    failureReason = ex.Message;
                    return false;
                }
            }

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

        private SwapExecutionAttempt TryExecuteSwap(
            PlannedEntryTransfer plannedEntry,
            IInventory fallbackTargetInventory,
            TransferExecutionOptions options)
        {
            var sourceSlot = plannedEntry.Entry.SourceSlot;
            var sourceInventory = plannedEntry.Entry.SourceInventory;
            var targetSlot = plannedEntry.SwapTargetSlot;
            var targetInventory = targetSlot?.Inventory ?? fallbackTargetInventory;

            if (sourceSlot == null || targetSlot == null || sourceInventory == null || targetInventory == null)
            {
                return new SwapExecutionAttempt(false, default, "Invalid swap target/source");
            }

            if (sourceSlot.IsEmpty || targetSlot.IsEmpty)
            {
                return new SwapExecutionAttempt(false, default, "Swap requires non-empty source and target slots");
            }

            if (!ValidateSwapRules(sourceSlot, sourceInventory, targetSlot, targetInventory, options?.GlobalRules, out var validationFailure))
            {
                return new SwapExecutionAttempt(false, default, validationFailure);
            }

            var sourceStackBefore = new ItemStack(sourceSlot.Stack.Item, sourceSlot.Stack.Count);
            var targetStackBefore = new ItemStack(targetSlot.Stack.Item, targetSlot.Stack.Count);

            var swapContext = new InventorySwapContext(
                sourceStackBefore,
                targetStackBefore,
                sourceSlot,
                targetSlot,
                sourceInventory,
                targetInventory);

            var swapDomainContexts = BuildSwapDomainContexts(
                sourceInventory,
                targetInventory,
                sourceSlot,
                targetSlot,
                sourceStackBefore,
                targetStackBefore);

            if (!ValidateDomainHandlers(swapDomainContexts[0], out validationFailure) ||
                !ValidateDomainHandlers(swapDomainContexts[1], out validationFailure))
            {
                return new SwapExecutionAttempt(false, default, validationFailure);
            }

            if (options?.SwapAttempting != null && !options.SwapAttempting(swapContext))
            {
                return new SwapExecutionAttempt(false, default, "Swap cancelled by listener");
            }

            if (targetInventory is not UniversalInventory targetUniversal)
            {
                return new SwapExecutionAttempt(false, default, "Target inventory must be UniversalInventory for swap");
            }

            var sourceUniversal = sourceInventory as UniversalInventory;
            if (!targetUniversal.TrySwapSlots(targetSlot, sourceSlot, out var swapResult))
            {
                return new SwapExecutionAttempt(false, default, "TrySwapSlots returned false");
            }

            swapDomainContexts[0].MarkCommitted(targetSlot, sourceStackBefore.Item, sourceStackBefore.Count);
            swapDomainContexts[1].MarkCommitted(sourceSlot, targetStackBefore.Item, targetStackBefore.Count);

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

        private async Task<SwapExecutionAttempt> TryExecuteSwapAsync(
            PlannedEntryTransfer plannedEntry,
            IInventory fallbackTargetInventory,
            TransferExecutionOptions options,
            CancellationToken cancellationToken)
        {
            var sourceSlot = plannedEntry.Entry.SourceSlot;
            var sourceInventory = plannedEntry.Entry.SourceInventory;
            var targetSlot = plannedEntry.SwapTargetSlot;
            var targetInventory = targetSlot?.Inventory ?? fallbackTargetInventory;

            if (sourceSlot == null || targetSlot == null || sourceInventory == null || targetInventory == null)
            {
                return new SwapExecutionAttempt(false, default, "Invalid swap target/source");
            }

            if (sourceSlot.IsEmpty || targetSlot.IsEmpty)
            {
                return new SwapExecutionAttempt(false, default, "Swap requires non-empty source and target slots");
            }

            if (!ValidateSwapRules(sourceSlot, sourceInventory, targetSlot, targetInventory, options?.GlobalRules, out var validationFailure))
            {
                return new SwapExecutionAttempt(false, default, validationFailure);
            }

            var sourceStackBefore = new ItemStack(sourceSlot.Stack.Item, sourceSlot.Stack.Count);
            var targetStackBefore = new ItemStack(targetSlot.Stack.Item, targetSlot.Stack.Count);

            var swapContext = new InventorySwapContext(
                sourceStackBefore,
                targetStackBefore,
                sourceSlot,
                targetSlot,
                sourceInventory,
                targetInventory);

            var swapDomainContexts = BuildSwapDomainContexts(
                sourceInventory,
                targetInventory,
                sourceSlot,
                targetSlot,
                sourceStackBefore,
                targetStackBefore);

            var forwardValidation = await ValidateDomainHandlersAsync(swapDomainContexts[0], true, cancellationToken);
            if (!forwardValidation.IsValid)
            {
                return new SwapExecutionAttempt(false, default, forwardValidation.FailureReason);
            }

            var reverseValidation = await ValidateDomainHandlersAsync(swapDomainContexts[1], true, cancellationToken);
            if (!reverseValidation.IsValid)
            {
                return new SwapExecutionAttempt(false, default, reverseValidation.FailureReason);
            }

            if (options?.SwapAttempting != null && !options.SwapAttempting(swapContext))
            {
                return new SwapExecutionAttempt(false, default, "Swap cancelled by listener");
            }

            if (targetInventory is not UniversalInventory targetUniversal)
            {
                return new SwapExecutionAttempt(false, default, "Target inventory must be UniversalInventory for swap");
            }

            var sourceUniversal = sourceInventory as UniversalInventory;
            if (!targetUniversal.TrySwapSlots(targetSlot, sourceSlot, out var swapResult))
            {
                return new SwapExecutionAttempt(false, default, "TrySwapSlots returned false");
            }

            swapDomainContexts[0].MarkCommitted(targetSlot, sourceStackBefore.Item, sourceStackBefore.Count);
            swapDomainContexts[1].MarkCommitted(sourceSlot, targetStackBefore.Item, targetStackBefore.Count);

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
            ISlot sourceSlot,
            ISlot targetSlot,
            ItemStack sourceStackBefore,
            ItemStack targetStackBefore)
        {
            return new[]
            {
                new TransferDomainContext(
                    sourceInventory,
                    targetInventory,
                    sourceSlot,
                    targetSlot,
                    sourceStackBefore.Item,
                    sourceStackBefore.Item,
                    sourceStackBefore.Count,
                    TransferKind.Swap),
                new TransferDomainContext(
                    targetInventory,
                    sourceInventory,
                    targetSlot,
                    sourceSlot,
                    targetStackBefore.Item,
                    targetStackBefore.Item,
                    targetStackBefore.Count,
                    TransferKind.Swap)
            };
        }

        private static TransferKind DetermineTransferKind(InventoryTransferRequest request)
        {
            if (request.TargetSlot != null && !request.TargetSlot.IsEmpty)
                return TransferKind.Merge;

            if (request.SourceSlot?.Stack != null && request.SourceSlot.Stack.Count > request.DraggedStack.Count)
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
            var sourceSlot = request.SourceSlot;
            var targetSlot = request.TargetSlot;
            var draggedStack = request.DraggedStack;

            var sourceSnapshotProvider = sourceInventory as IInventorySnapshotProvider;
            var targetSnapshotProvider = targetInventory as IInventorySnapshotProvider;

            var sourceInventorySnapshot = sourceSnapshotProvider?.CaptureSnapshot();
            var targetInventorySnapshot = targetSnapshotProvider?.CaptureSnapshot();
            var sourceSlotState = InventorySnapshotUtility.CaptureSlotState(sourceSlot);

            int requestedAmount = draggedStack.Count;
            var stackItem = draggedStack.Item;

            if (!TransferItemConversionUtility.TryResolveTargetItem(sourceInventory, targetInventory, stackItem, out var targetPreviewItem))
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Target inventory rejected item conversion</color>");
                return false;
            }

            var previewStack = new ItemStack(targetPreviewItem, requestedAmount);
            var acceptanceRequest = new InventoryAcceptanceRequest(
                targetInventory,
                targetPreviewItem,
                requestedAmount,
                new DragContext(previewStack, sourceSlot, sourceInventory, targetSlot, targetInventory),
                new DragEntry(previewStack, sourceSlot, sourceInventory));
            int acceptableCount = targetInventory.GetAcceptableCount(acceptanceRequest);

            if (acceptableCount <= 0)
            {
                Extensions.DragAndDropLog("<color=red>[TransferPlanExecutor] Target inventory cannot accept any items</color>");
                return false;
            }

            int transferAmount = Math.Min(requestedAmount, acceptableCount);
            int remainingAmount = requestedAmount - transferAmount;

            Extensions.DragAndDropLog($"<color=cyan>[TransferPlanExecutor] Requested: {requestedAmount}, Acceptable: {acceptableCount}, Transfer: {transferAmount}, Remaining: {remainingAmount}</color>");

            int removed = sourceSlot.Stack.RemoveFromStack(transferAmount);
            if (removed <= 0)
            {
                InventorySnapshotUtility.RestoreSlotState(sourceSlot, sourceSlotState);
                return false;
            }

            if (removed != transferAmount)
            {
                transferAmount = removed;
                remainingAmount = requestedAmount - transferAmount;
            }

            var transferStack = new ItemStack(stackItem, transferAmount);
            sourceSlot.UpdateVisuals();

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

            if (transferStack != null && !transferStack.IsEmpty)
            {
                Extensions.DragAndDropLog($"<color=yellow>[TransferPlanExecutor] {transferStack.Count} items not placed, returning to source</color>");
                if (sourceSlot.IsEmpty)
                {
                    sourceSlot.SetStack(new ItemStack(stackItem, transferStack.Count));
                }
                else
                {
                    sourceSlot.Stack.AddToStack(transferStack.Count);
                }

                sourceSlot.UpdateVisuals();
            }

            var resolvedSlot = operationContext.ResolvedSlot ?? targetSlot;
            bool targetWasEmpty = resolvedSlot != null && operationContext.TargetWasEmptyBefore;

            if (resolvedSlot == null && targetInventorySnapshot != null &&
                InventorySnapshotUtility.TryResolveSlotChange(targetInventory, targetInventorySnapshot, out var changedSlot, out var wasEmptyBefore))
            {
                resolvedSlot = changedSlot;
                targetWasEmpty = wasEmptyBefore;
            }

            result = new InventoryTransferResult(
                sourceInventory,
                targetInventory,
                sourceSlot,
                resolvedSlot,
                stackItem,
                resolvedSlot?.Stack?.Item ?? targetPreviewItem,
                actuallyAdded,
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

            if (operation.RequestedSlot != null)
            {
                bool wasEmpty = operation.RequestedSlot.IsEmpty;
                if (operation.TargetInventory.TryAddToSlot(
                        operation.TransferStack,
                        operation.RequestedSlot,
                        operation.SourceInventory,
                        operation.SourceSlot.Index,
                        operation.OperationContext))
                {
                    if (operation.OperationContext?.ResolvedSlot == null)
                    {
                        operation.OperationContext.RecordResult(operation.RequestedSlot, wasEmpty, operation.TransferAmount);
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

        private bool ValidateSwapRules(
            ISlot sourceSlot,
            IInventory sourceInventory,
            ISlot targetSlot,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            out string failureReason)
        {
            failureReason = null;

            var sourceStack = new ItemStack(sourceSlot.Stack.Item, sourceSlot.Stack.Count);
            var targetStack = new ItemStack(targetSlot.Stack.Item, targetSlot.Stack.Count);

            var sourceContext = new DragContext(sourceStack, sourceSlot, sourceInventory, targetSlot, targetInventory);
            var sourceEntry = sourceContext.Entries[0];

            var reverseContext = new DragContext(targetStack, targetSlot, targetInventory, sourceSlot, sourceInventory);
            var reverseEntry = reverseContext.Entries[0];

            var reverseStart = _ruleEvaluationService.ValidateEntryStart(reverseContext, reverseEntry, globalRules);
            if (!reverseStart.IsValid)
            {
                failureReason = reverseStart.FailureReason;
                return false;
            }

            var reverseDrop = _ruleEvaluationService.ValidateEntryDrop(reverseContext, reverseEntry, globalRules);
            if (!reverseDrop.IsValid)
            {
                failureReason = reverseDrop.FailureReason;
                return false;
            }

            var sourceDrop = _ruleEvaluationService.ValidateEntryDrop(sourceContext, sourceEntry, globalRules);
            if (!sourceDrop.IsValid)
            {
                failureReason = sourceDrop.FailureReason;
                return false;
            }

            return true;
        }

        private static void DispatchTransferEvents(IReadOnlyList<InventoryTransferResult> outcomes)
        {
            if (outcomes == null)
                return;

            foreach (var outcome in outcomes)
            {
                if (outcome.SourceItem == null || outcome.TargetItem == null || outcome.Amount <= 0)
                    continue;

                if (outcome.SourceInventory is UniversalInventory sourceUniversal)
                {
                    sourceUniversal.EmitItemRemoved(
                        outcome.SourceItem,
                        outcome.Amount,
                        outcome.SourceSlot?.Index ?? -1,
                        outcome.TargetInventory,
                        outcome.SourceSlot,
                        outcome.TargetSlot);
                    sourceUniversal.HandleSlotEmptied(outcome.SourceSlot);
                }

                if (outcome.TargetInventory is UniversalInventory targetUniversal && outcome.TargetSlot != null)
                {
                    targetUniversal.EmitItemAdded(
                        outcome.TargetItem,
                        outcome.Amount,
                        outcome.TargetSlot.Index,
                        outcome.SourceInventory,
                        outcome.SourceSlot,
                        outcome.TargetSlot);
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
                        outcome.SwapResult.TargetStackBefore.Item,
                        outcome.SwapResult.TargetStackBefore.Count,
                        outcome.TargetSlot.Index,
                        outcome.SourceInventory,
                        outcome.TargetSlot,
                        outcome.SourceSlot);

                    outcome.TargetInventory.EmitItemAdded(
                        outcome.SwapResult.SourceStackBefore.Item,
                        outcome.SwapResult.SourceStackBefore.Count,
                        outcome.TargetSlot.Index,
                        outcome.SourceInventory,
                        outcome.SourceSlot,
                        outcome.TargetSlot);
                }

                if (outcome.SourceInventory != null &&
                    outcome.SwapResult.SourceStackBefore != null &&
                    !outcome.SwapResult.SourceStackBefore.IsEmpty)
                {
                    outcome.SourceInventory.EmitItemRemoved(
                        outcome.SwapResult.SourceStackBefore.Item,
                        outcome.SwapResult.SourceStackBefore.Count,
                        outcome.SourceSlot.Index,
                        outcome.TargetInventory,
                        outcome.SourceSlot,
                        outcome.TargetSlot);

                    outcome.SourceInventory.EmitItemAdded(
                        outcome.SwapResult.TargetStackBefore.Item,
                        outcome.SwapResult.TargetStackBefore.Count,
                        outcome.SourceSlot.Index,
                        outcome.TargetInventory,
                        outcome.TargetSlot,
                        outcome.SourceSlot);
                }

                options?.SwapCompleted?.Invoke(outcome.SwapContext);
            }
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
                ISlot targetSlot,
                ISlot sourceSlot,
                SwapOperationResult swapResult,
                IReadOnlyList<TransferDomainContext> domainContexts)
            {
                SwapContext = swapContext;
                TargetInventory = targetInventory;
                SourceInventory = sourceInventory;
                TargetSlot = targetSlot;
                SourceSlot = sourceSlot;
                SwapResult = swapResult;
                DomainContexts = domainContexts;
            }

            public InventorySwapContext SwapContext { get; }
            public UniversalInventory TargetInventory { get; }
            public UniversalInventory SourceInventory { get; }
            public ISlot TargetSlot { get; }
            public ISlot SourceSlot { get; }
            public SwapOperationResult SwapResult { get; }
            public IReadOnlyList<TransferDomainContext> DomainContexts { get; }
            public int MovedAmount => SwapResult.SourceStackBefore?.Count ?? 0;
        }
    }
}
