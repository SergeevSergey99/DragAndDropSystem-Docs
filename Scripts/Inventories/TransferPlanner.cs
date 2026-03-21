using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    public enum TransferPlanFailureCode
    {
        None = 0,
        InvalidContext = 1,
        InvalidTargetInventory = 2,
        NoTargetSlots = 3,
        StrictTargetRejected = 4,
        NotEnoughCapacity = 5,
        UnsupportedPolicy = 6
    }

    public sealed class PlanFailure
    {
        public PlanFailure(TransferPlanFailureCode code, string reason, DragEntry? failedEntry = null)
        {
            Code = code;
            Reason = reason;
            FailedEntry = failedEntry;
        }

        public TransferPlanFailureCode Code { get; }
        public string Reason { get; }
        public DragEntry? FailedEntry { get; }
    }

    public readonly struct PlannedSlotAllocation
    {
        public PlannedSlotAllocation(ISlot slot, int amount)
        {
            Slot = slot;
            Amount = amount;
        }

        public ISlot Slot { get; }
        public int Amount { get; }
    }

    public sealed class PlannedEntryTransfer
    {
        public PlannedEntryTransfer(
            DragEntry entry,
            int requestedAmount,
            int plannedAmount,
            IReadOnlyList<PlannedSlotAllocation> allocations,
            string failureReason = null,
            bool requiresSwap = false,
            ISlot swapTargetSlot = null)
        {
            Entry = entry;
            RequestedAmount = requestedAmount;
            PlannedAmount = plannedAmount;
            Allocations = allocations;
            FailureReason = failureReason;
            RequiresSwap = requiresSwap;
            SwapTargetSlot = swapTargetSlot;
        }

        public DragEntry Entry { get; }
        public int RequestedAmount { get; }
        public int PlannedAmount { get; }
        public IReadOnlyList<PlannedSlotAllocation> Allocations { get; }
        public string FailureReason { get; }
        public bool RequiresSwap { get; }
        public ISlot SwapTargetSlot { get; }
        public bool IsPlanned => RequiresSwap || (PlannedAmount > 0 && Allocations.Count > 0);
        public bool IsPartial => PlannedAmount > 0 && PlannedAmount < RequestedAmount;
    }

    public sealed class TransferPlan
    {
        public TransferPlan(
            bool isValid,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            IReadOnlyList<PlannedEntryTransfer> entries,
            PlanFailure failure = null)
        {
            IsValid = isValid;
            Policy = policy;
            TargetInventory = targetInventory;
            TargetSlotHint = targetSlotHint;
            Entries = entries ?? new List<PlannedEntryTransfer>();
            Failure = failure;
        }

        public bool IsValid { get; }
        public DropPolicy Policy { get; }
        public IInventory TargetInventory { get; }
        public ISlot TargetSlotHint { get; }
        public IReadOnlyList<PlannedEntryTransfer> Entries { get; }
        public PlanFailure Failure { get; }

        public bool HasAnyTransfer
        {
            get
            {
                foreach (var entry in Entries)
                {
                    if (entry != null && entry.IsPlanned)
                        return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Планировщик переноса. Строит план без изменения состояния инвентарей.
    /// Выполнение плана будет добавлено отдельным executor-ом.
    /// </summary>
    public class TransferPlanner
    {
        private readonly RuleEvaluationService _ruleEvaluationService = new RuleEvaluationService();

        public TransferPlan BuildPlan(
            DragContext context,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            GlobalRuleValidator globalRules = null)
        {
            if (context == null || context.Entries == null || context.Entries.Count == 0)
            {
                return Fail(policy, targetInventory, targetSlotHint, TransferPlanFailureCode.InvalidContext, "Drag context is empty");
            }

            if (targetInventory == null)
            {
                return Fail(policy, targetInventory, targetSlotHint, TransferPlanFailureCode.InvalidTargetInventory, "Target inventory is null");
            }

            bool hasExistingSlots = targetInventory.Slots != null && targetInventory.Slots.Count > 0;
            bool canDeferSlotResolution = !hasExistingSlots && targetSlotHint == null;
            if (!hasExistingSlots && !canDeferSlotResolution)
            {
                return Fail(policy, targetInventory, targetSlotHint, TransferPlanFailureCode.NoTargetSlots, "Target inventory has no slots");
            }

            var effectivePolicy = policy ?? (context.IsBatchDrag ? DropPolicy.BatchAtomic : DropPolicy.SingleDefault);
            var virtualSlots = BuildVirtualSlots(targetInventory);
            var plannedEntries = new List<PlannedEntryTransfer>(context.Entries.Count);

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                var isFirstEntry = i == 0;

                var startValidation = _ruleEvaluationService.ValidateEntryStart(context, entry, globalRules);
                if (!startValidation.IsValid)
                {
                    var failedStart = new PlannedEntryTransfer(entry, entry.Stack?.Count ?? 0, 0, EmptyAllocations, startValidation.FailureReason);
                    plannedEntries.Add(failedStart);

                    if (effectivePolicy.BatchExecution == BatchExecutionPolicy.Atomic)
                    {
                        return Fail(
                            effectivePolicy,
                            targetInventory,
                            targetSlotHint,
                            TransferPlanFailureCode.StrictTargetRejected,
                            startValidation.FailureReason,
                            entry);
                    }

                    continue;
                }

                var planned = PlanEntry(
                    context,
                    entry,
                    effectivePolicy,
                    targetInventory,
                    targetSlotHint,
                    isFirstEntry,
                    virtualSlots,
                    globalRules);

                plannedEntries.Add(planned);

                if (!planned.IsPlanned)
                {
                    if (effectivePolicy.BatchExecution == BatchExecutionPolicy.Atomic)
                    {
                        return Fail(
                            effectivePolicy,
                            targetInventory,
                            targetSlotHint,
                            TransferPlanFailureCode.NotEnoughCapacity,
                            planned.FailureReason ?? "Entry cannot be placed",
                            entry);
                    }

                    continue;
                }

                if (planned.IsPartial && effectivePolicy.Capacity == CapacityPolicy.RejectAll)
                {
                    return Fail(
                        effectivePolicy,
                        targetInventory,
                        targetSlotHint,
                        TransferPlanFailureCode.NotEnoughCapacity,
                        "Partial planning is not allowed by policy",
                        entry);
                }
            }

            var plan = new TransferPlan(
                isValid: true,
                policy: effectivePolicy,
                targetInventory: targetInventory,
                targetSlotHint: targetSlotHint,
                entries: plannedEntries);

            if (!plan.HasAnyTransfer)
            {
                return Fail(
                    effectivePolicy,
                    targetInventory,
                    targetSlotHint,
                    TransferPlanFailureCode.NotEnoughCapacity,
                    "No entries could be planned");
            }

            return plan;
        }

        private PlannedEntryTransfer PlanEntry(
            DragContext context,
            DragEntry entry,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            bool isFirstEntry,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules)
        {
            if (entry.SourceInventory == null || entry.SourceSlot == null || entry.Stack == null || entry.Stack.IsEmpty)
            {
                return new PlannedEntryTransfer(entry, 0, 0, EmptyAllocations, "Invalid source entry");
            }

            int requested = entry.Stack.Count;
            var sourceItem = entry.Stack.Item;
            if (sourceItem == null || requested <= 0)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Invalid stack");
            }

            if (!TransferItemConversionUtility.TryResolveTargetItem(entry.SourceInventory, targetInventory, sourceItem, out var targetItem))
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Target inventory rejected item conversion");
            }

            int acceptableByInventory = targetInventory.GetAcceptableCount(targetItem, requested);
            if (acceptableByInventory <= 0)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Target inventory cannot accept this item");
            }

            var operation = new EntryPlanningOperation(
                context,
                entry,
                policy,
                targetInventory,
                targetSlotHint,
                preferHint: targetSlotHint != null && isFirstEntry,
                virtualSlots,
                globalRules,
                targetItem,
                requested,
                acceptableByInventory);

            // Inventory-area drop (no target slot) into dynamic inventory can start with 0 slots.
            // In this case actual slot is resolved during execution via TryAddStack/TryAddToSlot.
            if ((operation.VirtualSlots == null || operation.VirtualSlots.Count == 0) && operation.TargetSlotHint == null)
            {
                int deferredAmount = operation.Policy.Capacity == CapacityPolicy.Partial
                    ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                    : operation.RequestedAmount;

                if (deferredAmount <= 0)
                    return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No capacity for deferred placement");

                return new PlannedEntryTransfer(
                    entry,
                    requested,
                    deferredAmount,
                    new[] { new PlannedSlotAllocation(null, deferredAmount) });
            }

            var allocations = IsUniqueInventory(operation.TargetInventory)
                ? AllocateForUniqueInventory(operation)
                : AllocateForDefaultInventory(operation);

            int plannedAmount = 0;
            foreach (var allocation in allocations)
                plannedAmount += allocation.Amount;

            // Inventory-area drop into dynamic inventories:
            // when existing slots are all unsuitable/occupied, inventory may still accept items
            // by creating new slots during execution (TryAddStack path).
            if (plannedAmount == 0 && operation.TargetSlotHint == null && operation.AcceptableByInventory > 0)
            {
                int deferredAmount = operation.Policy.Capacity == CapacityPolicy.Partial
                    ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                    : operation.RequestedAmount;

                if (deferredAmount > 0)
                {
                    return new PlannedEntryTransfer(
                        entry,
                        requested,
                        deferredAmount,
                        new[] { new PlannedSlotAllocation(null, deferredAmount) });
                }
            }

            if (plannedAmount == 0)
            {
                if (ShouldPlanSwap(operation.Context, operation.Entry, operation.Policy, operation.TargetSlotHint, operation.PreferHint))
                {
                    return new PlannedEntryTransfer(
                        entry,
                        requested,
                        requested,
                        EmptyAllocations,
                        failureReason: null,
                        requiresSwap: true,
                        swapTargetSlot: targetSlotHint);
                }

                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No valid slot found for entry");
            }

            if (plannedAmount < requested && policy.Capacity == CapacityPolicy.RejectAll)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Entry cannot be placed fully");
            }

            return new PlannedEntryTransfer(entry, requested, plannedAmount, allocations);
        }

        private static bool ShouldPlanSwap(
            DragContext context,
            DragEntry entry,
            DropPolicy policy,
            ISlot targetSlotHint,
            bool preferHint)
        {
            if (!preferHint || targetSlotHint == null || policy == null)
                return false;

            if (policy.OccupiedTarget != OccupiedTargetPolicy.TrySwap)
                return false;

            if (context.IsBatchDrag)
                return false;

            return CanPlanSwap(entry, targetSlotHint);
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForDefaultInventory(EntryPlanningOperation operation)
        {
            int amountToAllocate = operation.Policy.Capacity == CapacityPolicy.Partial
                ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                : operation.RequestedAmount;

            var slot = ResolveSlot(operation, amountToAllocate);
            if (slot == null)
                return EmptyAllocations;

            slot.Apply(operation.TargetItem, amountToAllocate);
            return new[] { new PlannedSlotAllocation(slot.Slot, amountToAllocate) };
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForUniqueInventory(EntryPlanningOperation operation)
        {
            int desiredAmount = operation.Policy.Capacity == CapacityPolicy.Partial
                ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                : operation.RequestedAmount;

            var allocations = new List<PlannedSlotAllocation>(desiredAmount);

            // Для первого entry можно попробовать попасть в слот-хинт как в приоритетную цель.
            if (operation.PreferHint && operation.TargetSlotHint != null)
            {
                var preferred = FindVirtualSlot(operation.TargetSlotHint, operation.VirtualSlots);
                if (preferred != null &&
                    preferred.CanAccept(operation.TargetItem, uniqueMode: true) &&
                    IsCandidateAllowedByRules(operation, preferred.Slot, 1))
                {
                    preferred.Apply(operation.TargetItem, 1);
                    allocations.Add(new PlannedSlotAllocation(preferred.Slot, 1));
                }
                else if (operation.Policy.TargetUsage == TargetUsagePolicy.StrictTarget)
                {
                    return EmptyAllocations;
                }
                else if (operation.Policy.OccupiedTarget == OccupiedTargetPolicy.Reject)
                {
                    return EmptyAllocations;
                }
            }

            while (allocations.Count < desiredAmount)
            {
                var slot = FindNextAcceptingSlot(operation, 1, uniqueMode: true);
                if (slot == null)
                    break;

                slot.Apply(operation.TargetItem, 1);
                allocations.Add(new PlannedSlotAllocation(slot.Slot, 1));
            }

            return allocations;
        }

        private VirtualSlotState ResolveSlot(EntryPlanningOperation operation, int amountForValidation)
        {
            if (operation.PreferHint && operation.TargetSlotHint != null)
            {
                var hinted = FindVirtualSlot(operation.TargetSlotHint, operation.VirtualSlots);
                if (hinted != null &&
                    hinted.CanAccept(operation.TargetItem, uniqueMode: false) &&
                    IsCandidateAllowedByRules(operation, hinted.Slot, amountForValidation))
                    return hinted;

                if (operation.Policy.TargetUsage == TargetUsagePolicy.StrictTarget)
                    return null;

                if (operation.Policy.OccupiedTarget == OccupiedTargetPolicy.Reject)
                    return null;

                if (operation.Policy.OccupiedTarget == OccupiedTargetPolicy.TrySwap)
                    return null; // swap будет поддержан на этапе executor/policy resolution
            }

            return FindNextAcceptingSlot(operation, amountForValidation, uniqueMode: false);
        }

        private static VirtualSlotState FindVirtualSlot(ISlot slot, IReadOnlyList<VirtualSlotState> states)
        {
            if (slot == null || states == null)
                return null;

            foreach (var state in states)
            {
                if (ReferenceEquals(state.Slot, slot))
                    return state;
            }

            return null;
        }

        private VirtualSlotState FindNextAcceptingSlot(
            EntryPlanningOperation operation,
            int amountForValidation,
            bool uniqueMode,
            ISlot preferredSlot = null)
        {
            if (preferredSlot != null)
            {
                foreach (var state in operation.VirtualSlots)
                {
                    if (ReferenceEquals(state.Slot, preferredSlot) &&
                        state.CanAccept(operation.TargetItem, uniqueMode) &&
                        IsCandidateAllowedByRules(operation, state.Slot, amountForValidation))
                        return state;
                }
            }

            foreach (var state in operation.VirtualSlots)
            {
                if (state.CanAccept(operation.TargetItem, uniqueMode) &&
                    IsCandidateAllowedByRules(operation, state.Slot, amountForValidation))
                    return state;
            }

            return null;
        }

        private static IReadOnlyList<VirtualSlotState> BuildVirtualSlots(IInventory inventory)
        {
            var result = new List<VirtualSlotState>(inventory.Slots.Count);

            foreach (var slot in inventory.Slots)
            {
                if (slot == null)
                    continue;
                result.Add(new VirtualSlotState(slot));
            }

            return result;
        }

        private static bool IsUniqueInventory(IInventory inventory) =>
            inventory is UniversalInventory universal &&
            universal.UsesPerItemSlotPlanning();

        private static int Min(int a, int b) => a < b ? a : b;

        private static bool CanPlanSwap(DragEntry entry, ISlot targetSlot)
        {
            if (entry.SourceSlot == null || targetSlot == null)
                return false;

            if (ReferenceEquals(entry.SourceSlot, targetSlot))
                return false;

            if (targetSlot.IsEmpty)
                return false;

            if (entry.SourceSlot.IsEmpty || entry.SourceSlot.Stack == null || entry.SourceSlot.Stack.IsEmpty)
                return false;

            if (entry.Stack == null || entry.Stack.IsEmpty || entry.Stack.Item == null)
                return false;

            // Current swap implementation supports only full stack from source slot.
            return entry.SourceSlot.Stack.Count == entry.Stack.Count;
        }

        private bool IsCandidateAllowedByRules(
            EntryPlanningOperation operation,
            ISlot targetSlot,
            int plannedAmount)
        {
            if (plannedAmount <= 0 || operation.TargetItem == null)
                return false;

            DragEntry validationEntry = operation.Entry;
            if (operation.Entry.Stack == null ||
                operation.Entry.Stack.Item == null ||
                operation.Entry.Stack.Count != plannedAmount ||
                !ReferenceEquals(operation.Entry.Stack.Item, operation.TargetItem))
            {
                validationEntry = new DragEntry(
                    new ItemStack(operation.TargetItem, plannedAmount),
                    operation.Entry.SourceSlot,
                    operation.Entry.SourceInventory);
            }

            var result = _ruleEvaluationService.ValidateEntryDrop(
                operation.Context.WithTarget(targetSlot, operation.TargetInventory),
                validationEntry,
                operation.GlobalRules);
            return result.IsValid;
        }

        private static readonly IReadOnlyList<PlannedSlotAllocation> EmptyAllocations = new PlannedSlotAllocation[0];

        private static TransferPlan Fail(
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            TransferPlanFailureCode code,
            string reason,
            DragEntry? failedEntry = null)
        {
            return new TransferPlan(
                isValid: false,
                policy: policy ?? DropPolicy.SingleDefault,
                targetInventory: targetInventory,
                targetSlotHint: targetSlotHint,
                entries: new List<PlannedEntryTransfer>(),
                failure: new PlanFailure(code, reason, failedEntry));
        }

    }
}
