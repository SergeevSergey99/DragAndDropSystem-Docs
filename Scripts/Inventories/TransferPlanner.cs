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

            if (targetInventory.Slots == null || targetInventory.Slots.Count == 0)
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
            var item = entry.Stack.Item;
            if (item == null || requested <= 0)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Invalid stack");
            }

            int acceptableByInventory = targetInventory.GetAcceptableCount(item, requested);
            if (acceptableByInventory <= 0)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Target inventory cannot accept this item");
            }

            bool preferHint = targetSlotHint != null && isFirstEntry;
            var allocations = IsUniqueInventory(targetInventory)
                ? AllocateForUniqueInventory(context, entry, item, requested, acceptableByInventory, policy, targetInventory, targetSlotHint, preferHint, virtualSlots, globalRules)
                : AllocateForDefaultInventory(context, entry, item, requested, acceptableByInventory, policy, targetInventory, targetSlotHint, preferHint, virtualSlots, globalRules);

            int plannedAmount = 0;
            foreach (var allocation in allocations)
                plannedAmount += allocation.Amount;

            if (plannedAmount == 0)
            {
                if (ShouldPlanSwap(context, entry, policy, targetSlotHint, preferHint))
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

        private IReadOnlyList<PlannedSlotAllocation> AllocateForDefaultInventory(
            DragContext context,
            DragEntry entry,
            IInventoryItem item,
            int requested,
            int acceptableByInventory,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules)
        {
            int amountToAllocate = policy.Capacity == CapacityPolicy.Partial
                ? Min(requested, acceptableByInventory)
                : requested;

            var slot = ResolveSlot(context, entry, item, amountToAllocate, policy, targetInventory, targetSlotHint, preferHint, virtualSlots, globalRules);
            if (slot == null)
                return EmptyAllocations;

            slot.Apply(item, amountToAllocate);
            return new[] { new PlannedSlotAllocation(slot.Slot, amountToAllocate) };
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForUniqueInventory(
            DragContext context,
            DragEntry entry,
            IInventoryItem item,
            int requested,
            int acceptableByInventory,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules)
        {
            int desiredAmount = policy.Capacity == CapacityPolicy.Partial
                ? Min(requested, acceptableByInventory)
                : requested;

            var allocations = new List<PlannedSlotAllocation>(desiredAmount);

            // Для первого entry можно попробовать попасть в слот-хинт как в приоритетную цель.
            if (preferHint && targetSlotHint != null)
            {
                var preferred = FindVirtualSlot(targetSlotHint, virtualSlots);
                if (preferred != null &&
                    preferred.CanAccept(item, uniqueMode: true) &&
                    IsCandidateAllowedByRules(context, entry, targetInventory, preferred.Slot, globalRules, 1))
                {
                    preferred.Apply(item, 1);
                    allocations.Add(new PlannedSlotAllocation(preferred.Slot, 1));
                }
                else if (policy.TargetUsage == TargetUsagePolicy.StrictTarget)
                {
                    return EmptyAllocations;
                }
                else if (policy.OccupiedTarget == OccupiedTargetPolicy.Reject)
                {
                    return EmptyAllocations;
                }
            }

            while (allocations.Count < desiredAmount)
            {
                var slot = FindNextAcceptingSlot(context, entry, targetInventory, item, 1, virtualSlots, uniqueMode: true, preferredSlot: null, globalRules);
                if (slot == null)
                    break;

                slot.Apply(item, 1);
                allocations.Add(new PlannedSlotAllocation(slot.Slot, 1));
            }

            return allocations;
        }

        private VirtualSlotState ResolveSlot(
            DragContext context,
            DragEntry entry,
            IInventoryItem item,
            int amountForValidation,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules)
        {
            if (preferHint && targetSlotHint != null)
            {
                var hinted = FindVirtualSlot(targetSlotHint, virtualSlots);
                if (hinted != null &&
                    hinted.CanAccept(item, uniqueMode: false) &&
                    IsCandidateAllowedByRules(context, entry, targetInventory, hinted.Slot, globalRules, amountForValidation))
                    return hinted;

                if (policy.TargetUsage == TargetUsagePolicy.StrictTarget)
                    return null;

                if (policy.OccupiedTarget == OccupiedTargetPolicy.Reject)
                    return null;

                if (policy.OccupiedTarget == OccupiedTargetPolicy.TrySwap)
                    return null; // swap будет поддержан на этапе executor/policy resolution
            }

            return FindNextAcceptingSlot(context, entry, targetInventory, item, amountForValidation, virtualSlots, uniqueMode: false, preferredSlot: null, globalRules);
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
            DragContext context,
            DragEntry entry,
            IInventory targetInventory,
            IInventoryItem item,
            int amountForValidation,
            IReadOnlyList<VirtualSlotState> states,
            bool uniqueMode,
            ISlot preferredSlot,
            GlobalRuleValidator globalRules)
        {
            if (preferredSlot != null)
            {
                foreach (var state in states)
                {
                    if (ReferenceEquals(state.Slot, preferredSlot) &&
                        state.CanAccept(item, uniqueMode) &&
                        IsCandidateAllowedByRules(context, entry, targetInventory, state.Slot, globalRules, amountForValidation))
                        return state;
                }
            }

            foreach (var state in states)
            {
                if (state.CanAccept(item, uniqueMode) &&
                    IsCandidateAllowedByRules(context, entry, targetInventory, state.Slot, globalRules, amountForValidation))
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
            universal.ItemBehavior == UniversalInventory.ItemBehaviorType.Unique;

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
            DragContext context,
            DragEntry entry,
            IInventory targetInventory,
            ISlot targetSlot,
            GlobalRuleValidator globalRules,
            int plannedAmount)
        {
            if (plannedAmount <= 0 || entry.Stack == null || entry.Stack.Item == null)
                return false;

            DragEntry validationEntry = entry;
            if (entry.Stack.Count != plannedAmount)
            {
                validationEntry = new DragEntry(
                    new ItemStack(entry.Stack.Item, plannedAmount),
                    entry.SourceSlot,
                    entry.SourceInventory);
            }

            var result = _ruleEvaluationService.ValidateEntryDrop(context.WithTarget(targetSlot, targetInventory), validationEntry, globalRules);
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

        private sealed class VirtualSlotState
        {
            public VirtualSlotState(ISlot slot)
            {
                Slot = slot;
                if (slot == null || slot.IsEmpty || slot.Stack?.Item == null)
                {
                    _item = null;
                    _count = 0;
                    return;
                }

                _item = slot.Stack.Item;
                _count = slot.Stack.Count;
            }

            private IInventoryItem _item;
            private int _count;

            public ISlot Slot { get; }
            public bool IsEmpty => _item == null || _count <= 0;

            public bool CanAccept(IInventoryItem item, bool uniqueMode)
            {
                if (Slot == null || item == null || !Slot.IsInteractable)
                    return false;

                if (uniqueMode)
                    return IsEmpty;

                if (IsEmpty)
                    return true;

                return _item.ItemId == item.ItemId;
            }

            public void Apply(IInventoryItem item, int amount)
            {
                if (amount <= 0 || item == null)
                    return;

                if (IsEmpty)
                {
                    _item = item;
                    _count = amount;
                }
                else
                {
                    _count += amount;
                }
            }
        }
    }
}
