using System.Collections.Generic;
using DragAndDropSystem.Core;
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
            string failureReason = null)
        {
            Entry = entry;
            RequestedAmount = requestedAmount;
            PlannedAmount = plannedAmount;
            Allocations = allocations;
            FailureReason = failureReason;
        }

        public DragEntry Entry { get; }
        public int RequestedAmount { get; }
        public int PlannedAmount { get; }
        public IReadOnlyList<PlannedSlotAllocation> Allocations { get; }
        public string FailureReason { get; }
        public bool IsPlanned => PlannedAmount > 0 && Allocations.Count > 0;
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
        public TransferPlan BuildPlan(
            DragContext context,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint)
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

                var planned = PlanEntry(
                    entry,
                    effectivePolicy,
                    targetInventory,
                    targetSlotHint,
                    isFirstEntry,
                    virtualSlots);

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
            DragEntry entry,
            DropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            bool isFirstEntry,
            IReadOnlyList<VirtualSlotState> virtualSlots)
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
                ? AllocateForUniqueInventory(item, requested, acceptableByInventory, policy, targetSlotHint, preferHint, virtualSlots)
                : AllocateForDefaultInventory(item, requested, acceptableByInventory, policy, targetSlotHint, preferHint, virtualSlots);

            int plannedAmount = 0;
            foreach (var allocation in allocations)
                plannedAmount += allocation.Amount;

            if (plannedAmount == 0)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No valid slot found for entry");
            }

            if (plannedAmount < requested && policy.Capacity == CapacityPolicy.RejectAll)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Entry cannot be placed fully");
            }

            return new PlannedEntryTransfer(entry, requested, plannedAmount, allocations);
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForDefaultInventory(
            IInventoryItem item,
            int requested,
            int acceptableByInventory,
            DropPolicy policy,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots)
        {
            int amountToAllocate = policy.Capacity == CapacityPolicy.Partial
                ? Min(requested, acceptableByInventory)
                : requested;

            var slot = ResolveSlot(item, policy, targetSlotHint, preferHint, virtualSlots);
            if (slot == null)
                return EmptyAllocations;

            slot.Apply(item, amountToAllocate);
            return new[] { new PlannedSlotAllocation(slot.Slot, amountToAllocate) };
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForUniqueInventory(
            IInventoryItem item,
            int requested,
            int acceptableByInventory,
            DropPolicy policy,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots)
        {
            int desiredAmount = policy.Capacity == CapacityPolicy.Partial
                ? Min(requested, acceptableByInventory)
                : requested;

            var allocations = new List<PlannedSlotAllocation>(desiredAmount);

            // Для первого entry можно попробовать попасть в слот-хинт как в приоритетную цель.
            if (preferHint && targetSlotHint != null)
            {
                var preferred = FindVirtualSlot(targetSlotHint, virtualSlots);
                if (preferred != null && preferred.CanAccept(item, uniqueMode: true))
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
                var slot = FindNextAcceptingSlot(item, virtualSlots, uniqueMode: true, preferredSlot: null);
                if (slot == null)
                    break;

                slot.Apply(item, 1);
                allocations.Add(new PlannedSlotAllocation(slot.Slot, 1));
            }

            return allocations;
        }

        private VirtualSlotState ResolveSlot(
            IInventoryItem item,
            DropPolicy policy,
            ISlot targetSlotHint,
            bool preferHint,
            IReadOnlyList<VirtualSlotState> virtualSlots)
        {
            if (preferHint && targetSlotHint != null)
            {
                var hinted = FindVirtualSlot(targetSlotHint, virtualSlots);
                if (hinted != null && hinted.CanAccept(item, uniqueMode: false))
                    return hinted;

                if (policy.TargetUsage == TargetUsagePolicy.StrictTarget)
                    return null;

                if (policy.OccupiedTarget == OccupiedTargetPolicy.Reject)
                    return null;

                if (policy.OccupiedTarget == OccupiedTargetPolicy.TrySwap)
                    return null; // swap будет поддержан на этапе executor/policy resolution
            }

            return FindNextAcceptingSlot(item, virtualSlots, uniqueMode: false, preferredSlot: null);
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

        private static VirtualSlotState FindNextAcceptingSlot(
            IInventoryItem item,
            IReadOnlyList<VirtualSlotState> states,
            bool uniqueMode,
            ISlot preferredSlot)
        {
            if (preferredSlot != null)
            {
                foreach (var state in states)
                {
                    if (ReferenceEquals(state.Slot, preferredSlot) && state.CanAccept(item, uniqueMode))
                        return state;
                }
            }

            foreach (var state in states)
            {
                if (state.CanAccept(item, uniqueMode))
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
