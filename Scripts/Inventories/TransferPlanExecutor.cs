using System.Collections.Generic;
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
        private readonly InventoryTransferService _transferService;
        private readonly RuleEvaluationService _ruleEvaluationService = new RuleEvaluationService();

        public TransferPlanExecutor(InventoryTransferService transferService = null)
        {
            _transferService = transferService ?? new InventoryTransferService();
        }

        public TransferExecutionSummary Execute(TransferPlan plan, TransferExecutionOptions options = null)
        {
            if (plan == null || !plan.IsValid || plan.Entries == null || plan.Entries.Count == 0)
            {
                var failed = DropResult.Failed("Invalid transfer plan");
                return new TransferExecutionSummary(false, 0, 0, 0, false, failed);
            }

            bool atomic = plan.Policy.BatchExecution == BatchExecutionPolicy.Atomic;
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
                    if (TryExecuteSwap(plannedEntry, plan.TargetInventory, options, out var swapOutcome, out var swapFailure))
                    {
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
                        Extentions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Swap failed: {swapFailure}</color>");
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
                            new ItemStack(plannedEntry.Entry.Stack.Item, allocation.Amount),
                            allowAlternativeSlots: false);

                        string domainFailure = null;
                        if (!TryBuildDomainContext(request, plannedEntry.PreviewTargetItem, out var domainContext))
                        {
                            domainFailure = "Failed to build domain context";
                            Extentions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain validation failed: {domainFailure}</color>");
                            entryFailed = true;
                            break;
                        }

                        if (!ValidateDomainHandlers(domainContext, out domainFailure))
                        {
                            Extentions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain validation failed: {domainFailure}</color>");
                            entryFailed = true;
                            break;
                        }

                        if (!_transferService.TryExecuteTransfer(request, out var outcome) || outcome.Amount <= 0)
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

            bool success = succeededEntries > 0 && (failedEntries == 0 || plan.Policy.BatchExecution == BatchExecutionPolicy.BestEffort);
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

            Extentions.DragAndDropLog($"<color=green>[TransferPlanExecutor] Executed plan: successEntries={succeededEntries}, failedEntries={failedEntries}, amount={transferredAmount}</color>");
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
                        Extentions.DragAndDropLog($"<color=red>[TransferPlanExecutor] Domain success hook threw: {ex.Message}</color>");
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

        private bool TryExecuteSwap(
            PlannedEntryTransfer plannedEntry,
            IInventory fallbackTargetInventory,
            TransferExecutionOptions options,
            out PendingSwapOutcome swapOutcome,
            out string failureReason)
        {
            swapOutcome = default;
            failureReason = null;

            var sourceSlot = plannedEntry.Entry.SourceSlot;
            var sourceInventory = plannedEntry.Entry.SourceInventory;
            var targetSlot = plannedEntry.SwapTargetSlot;
            var targetInventory = targetSlot?.Inventory ?? fallbackTargetInventory;

            if (sourceSlot == null || targetSlot == null || sourceInventory == null || targetInventory == null)
            {
                failureReason = "Invalid swap target/source";
                return false;
            }

            if (sourceSlot.IsEmpty || targetSlot.IsEmpty)
            {
                failureReason = "Swap requires non-empty source and target slots";
                return false;
            }

            if (!ValidateSwapRules(sourceSlot, sourceInventory, targetSlot, targetInventory, options?.GlobalRules, out var validationFailure))
            {
                failureReason = validationFailure;
                return false;
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
                failureReason = validationFailure;
                return false;
            }

            if (options?.SwapAttempting != null && !options.SwapAttempting(swapContext))
            {
                failureReason = "Swap cancelled by listener";
                return false;
            }

            if (targetInventory is not UniversalInventory targetUniversal)
            {
                failureReason = "Target inventory must be UniversalInventory for swap";
                return false;
            }

            var sourceUniversal = sourceInventory as UniversalInventory;
            if (!targetUniversal.TrySwapSlots(targetSlot, sourceSlot, out var swapResult))
            {
                failureReason = "TrySwapSlots returned false";
                return false;
            }

            swapDomainContexts[0].MarkCommitted(targetSlot, sourceStackBefore.Item, sourceStackBefore.Count);
            swapDomainContexts[1].MarkCommitted(sourceSlot, targetStackBefore.Item, targetStackBefore.Count);

            swapOutcome = new PendingSwapOutcome(
                swapContext,
                targetUniversal,
                sourceUniversal,
                targetSlot,
                sourceSlot,
                swapResult,
                swapDomainContexts);
            return true;
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
