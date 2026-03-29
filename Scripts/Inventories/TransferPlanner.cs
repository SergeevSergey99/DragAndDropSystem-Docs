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
            ISlot swapTargetSlot = null,
            IInventoryItem previewTargetItem = null,
            bool requiresOccupiedHandler = false,
            ISlot occupiedTargetSlot = null)
        {
            Entry = entry;
            RequestedAmount = requestedAmount;
            PlannedAmount = plannedAmount;
            Allocations = allocations;
            FailureReason = failureReason;
            RequiresSwap = requiresSwap;
            SwapTargetSlot = swapTargetSlot;
            PreviewTargetItem = previewTargetItem ?? entry.Stack?.Item;
            RequiresOccupiedHandler = requiresOccupiedHandler;
            OccupiedTargetSlot = occupiedTargetSlot;
        }

        public DragEntry Entry { get; }
        public int RequestedAmount { get; }
        public int PlannedAmount { get; }
        public IReadOnlyList<PlannedSlotAllocation> Allocations { get; }
        public string FailureReason { get; }
        public bool RequiresSwap { get; }
        public ISlot SwapTargetSlot { get; }
        public IInventoryItem PreviewTargetItem { get; }
        public bool RequiresOccupiedHandler { get; }
        public ISlot OccupiedTargetSlot { get; }
        public bool IsPlanned => RequiresSwap || RequiresOccupiedHandler || (PlannedAmount > 0 && Allocations.Count > 0);
        public bool IsPartial => PlannedAmount > 0 && PlannedAmount < RequestedAmount;
    }

    public sealed class TransferPlan
    {
        public TransferPlan(
            bool isValid,
            ResolvedDropPolicy policy,
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
        public ResolvedDropPolicy Policy { get; }
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
            ResolvedDropPolicy policy,
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

                    if (policy.BatchMode == BatchMode.Atomic)
                    {
                        return Fail(
                            policy,
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
                    policy,
                    targetInventory,
                    targetSlotHint,
                    isFirstEntry,
                    virtualSlots,
                    globalRules);

                plannedEntries.Add(planned);

                if (!planned.IsPlanned)
                {
                    if (policy.BatchMode == BatchMode.Atomic)
                    {
                        return Fail(
                            policy,
                            targetInventory,
                            targetSlotHint,
                            TransferPlanFailureCode.NotEnoughCapacity,
                            planned.FailureReason ?? "Entry cannot be placed",
                            entry);
                    }

                    continue;
                }

                if (planned.IsPartial && !policy.AllowPartial)
                {
                    return Fail(
                        policy,
                        targetInventory,
                        targetSlotHint,
                        TransferPlanFailureCode.NotEnoughCapacity,
                        "Partial planning is not allowed by policy",
                        entry);
                }
            }

            var plan = new TransferPlan(
                isValid: true,
                policy: policy,
                targetInventory: targetInventory,
                targetSlotHint: targetSlotHint,
                entries: plannedEntries);

            if (!plan.HasAnyTransfer)
            {
                return Fail(
                    policy,
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
            ResolvedDropPolicy policy,
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

            var acceptanceRequest = new InventoryAcceptanceRequest(
                targetInventory,
                targetItem,
                requested,
                context,
                entry);
            int acceptableByInventory = targetInventory.GetAcceptableCount(acceptanceRequest);
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
                int deferredAmount = operation.Policy.AllowPartial
                    ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                    : operation.RequestedAmount;

                if (deferredAmount <= 0)
                    return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No capacity for deferred placement");

                return new PlannedEntryTransfer(
                    entry,
                    requested,
                    deferredAmount,
                    new[] { new PlannedSlotAllocation(null, deferredAmount) },
                    previewTargetItem: targetItem);
            }

            var allocations = IsUniqueInventory(operation.TargetInventory)
                ? AllocateForUniqueInventory(operation)
                : AllocateForStrategyInventory(operation);

            int plannedAmount = 0;
            foreach (var allocation in allocations)
                plannedAmount += allocation.Amount;

            // Inventory-area drop into dynamic inventories:
            // when existing slots are all unsuitable/occupied, inventory may still accept items
            // by creating new slots during execution (TryAddStack path).
            if (plannedAmount == 0 && operation.TargetSlotHint == null && operation.AcceptableByInventory > 0)
            {
                int deferredAmount = operation.Policy.AllowPartial
                    ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                    : operation.RequestedAmount;

                if (deferredAmount > 0)
                {
                    return new PlannedEntryTransfer(
                        entry,
                        requested,
                        deferredAmount,
                        new[] { new PlannedSlotAllocation(null, deferredAmount) },
                        previewTargetItem: targetItem);
                }
            }

            if (plannedAmount == 0)
            {
                if (isFirstEntry && targetSlotHint != null && !targetSlotHint.IsEmpty
                    && targetInventory is UniversalInventory occupiedUni
                    && occupiedUni.CheckOccupiedSlotDrop(entry, targetSlotHint))
                {
                    return new PlannedEntryTransfer(
                        entry,
                        requested,
                        requested,
                        EmptyAllocations,
                        previewTargetItem: targetItem,
                        requiresOccupiedHandler: true,
                        occupiedTargetSlot: targetSlotHint);
                }

                if (ShouldPlanSwap(operation.Context, operation.Entry, operation.Policy, operation.TargetSlotHint, operation.PreferHint))
                {
                    return new PlannedEntryTransfer(
                        entry,
                        requested,
                        requested,
                        EmptyAllocations,
                        failureReason: null,
                        requiresSwap: true,
                        swapTargetSlot: targetSlotHint,
                        previewTargetItem: targetItem);
                }

                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No valid slot found for entry");
            }

            if (plannedAmount < requested && !policy.AllowPartial)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Entry cannot be placed fully");
            }

            return new PlannedEntryTransfer(entry, requested, plannedAmount, allocations, previewTargetItem: targetItem);
        }

        private static bool ShouldPlanSwap(
            DragContext context,
            DragEntry entry,
            ResolvedDropPolicy policy,
            ISlot targetSlotHint,
            bool preferHint)
        {
            if (!preferHint || targetSlotHint == null)
                return false;

            if (policy.BlockedTarget != BlockedTargetBehavior.Swap)
                return false;

            if (context.IsBatchDrag)
                return false;

            return CanPlanSwap(entry, targetSlotHint);
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForStrategyInventory(EntryPlanningOperation operation)
        {
            bool canSearchAlternatives = CanSearchAlternativeSlots(operation);
            int totalToAllocate = operation.Policy.AllowPartial
                ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                : operation.RequestedAmount;

            if (totalToAllocate <= 0)
                return EmptyAllocations;

            var allocations = new List<PlannedSlotAllocation>();
            int remaining = totalToAllocate;
            bool anyPlaced = false;

            if (operation.PreferHint && operation.TargetSlotHint != null)
            {
                var hinted = FindVirtualSlot(operation.TargetSlotHint, operation.VirtualSlots);
                int placedIntoHint = TryAllocateIntoSlot(operation, hinted, remaining, allocations, uniqueMode: false);
                if (placedIntoHint > 0)
                {
                    anyPlaced = true;
                    remaining -= placedIntoHint;
                }

                if (remaining <= 0)
                    return allocations;

                if (!anyPlaced && operation.Policy.BlockedTarget == BlockedTargetBehavior.Swap)
                    return EmptyAllocations;

                if (!canSearchAlternatives)
                    return allocations;
            }

            if (remaining <= 0 || !canSearchAlternatives)
                return allocations;

            ISlot excludeFromAlternatives = operation.PreferHint ? operation.TargetSlotHint : null;
            var candidates = EnumerateAlternativeVirtualSlots(operation, excludeFromAlternatives);
            foreach (var candidate in candidates)
            {
                int placed = TryAllocateIntoSlot(operation, candidate, remaining, allocations, uniqueMode: false);
                if (placed <= 0)
                    continue;

                anyPlaced = true;
                remaining -= placed;
                if (remaining <= 0)
                    break;
            }

            return allocations;
        }

        private IReadOnlyList<PlannedSlotAllocation> AllocateForUniqueInventory(EntryPlanningOperation operation)
        {
            bool canSearchAlternatives = CanSearchAlternativeSlots(operation);
            int desiredAmount = operation.Policy.AllowPartial
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
                else if (!canSearchAlternatives)
                {
                    return EmptyAllocations;
                }
            }

            if (allocations.Count > 0 &&
                operation.PreferHint &&
                operation.TargetSlotHint != null &&
                !canSearchAlternatives)
                return allocations;

            if (!canSearchAlternatives)
                return allocations;

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

        private int TryAllocateIntoSlot(
            EntryPlanningOperation operation,
            VirtualSlotState slot,
            int desiredAmount,
            List<PlannedSlotAllocation> allocations,
            bool uniqueMode)
        {
            if (slot == null || desiredAmount <= 0)
                return 0;

            int capacity = GetSlotPlacementCapacity(operation, slot, desiredAmount, uniqueMode);
            if (capacity <= 0)
                return 0;

            if (!IsCandidateAllowedByRules(operation, slot.Slot, capacity))
                return 0;

            slot.Apply(operation.TargetItem, capacity);
            allocations.Add(new PlannedSlotAllocation(slot.Slot, capacity));
            return capacity;
        }

        private int GetSlotPlacementCapacity(
            EntryPlanningOperation operation,
            VirtualSlotState slot,
            int desiredAmount,
            bool uniqueMode)
        {
            if (slot == null || desiredAmount <= 0 || operation.TargetItem == null)
                return 0;

            if (!CanStrategyPlaceIntoSlot(operation, slot, uniqueMode))
                return 0;

            if (uniqueMode)
                return 1;

            if (slot.IsEmpty)
            {
                return Min(desiredAmount, GetMaxStackSize(operation.TargetInventory, operation.TargetItem));
            }

            int maxStack = GetMaxStackSize(operation.TargetInventory, operation.TargetItem);
            if (maxStack <= slot.Count)
                return 0;

            return Min(desiredAmount, maxStack - slot.Count);
        }

        private static bool CanSearchAlternativeSlots(EntryPlanningOperation operation)
        {
            if (operation.Policy.BlockedTarget != BlockedTargetBehavior.FindAlternative)
                return false;

            return !ReferenceEquals(operation.Entry.SourceInventory, operation.TargetInventory);
        }

        private bool CanStrategyPlaceIntoSlot(
            EntryPlanningOperation operation,
            VirtualSlotState slot,
            bool uniqueMode)
        {
            if (slot == null || operation.TargetItem == null)
                return false;

            if (uniqueMode)
                return slot.CanAccept(operation.TargetItem, uniqueMode: true);

            if (slot.IsEmpty)
                return slot.CanAccept(operation.TargetItem, uniqueMode: false);

            // Virtual slot is occupied: ensure item compatibility at virtual level first.
            // Without this, CanUseAlternativeSlot checks the real (potentially empty) slot
            // and may incorrectly allow placing a different item into a virtually occupied slot.
            if (!slot.CanAccept(operation.TargetItem, uniqueMode: false))
                return false;

            var placementStrategy = ResolvePlacementStrategy(operation.TargetInventory);
            if (placementStrategy == null)
                return true;

            return placementStrategy.CanUseAlternativeSlot(slot.Slot, operation.TargetItem);
        }

        private IEnumerable<VirtualSlotState> EnumerateAlternativeVirtualSlots(EntryPlanningOperation operation, ISlot excludeSlot)
        {
            if (operation.VirtualSlots == null || operation.VirtualSlots.Count == 0)
                yield break;

            var placementStrategy = ResolvePlacementStrategy(operation.TargetInventory);
            if (placementStrategy == null)
                yield break;

            IEnumerable<ISlot> orderedSlots = placementStrategy.EnumerateAlternativeSlots(
                GetInventorySlots(operation.TargetInventory),
                operation.TargetItem,
                operation.Policy.AlternativePlacement,
                excludeSlot);

            if (orderedSlots == null)
                yield break;

            foreach (var orderedSlot in orderedSlots)
            {
                var state = FindVirtualSlot(orderedSlot, operation.VirtualSlots);
                if (state != null)
                    yield return state;
            }
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
            universal.PlacementStrategy.UsesPerItemSlotPlanning;

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

        private static IPlacementStrategy ResolvePlacementStrategy(IInventory inventory)
        {
            var universal = inventory as UniversalInventory;
            return universal != null ? universal.PlacementStrategy : null;
        }

        private static List<ISlot> GetInventorySlots(IInventory inventory)
        {
            if (inventory == null || inventory.Slots == null)
                return null;

            return inventory.Slots as List<ISlot> ?? new List<ISlot>(inventory.Slots);
        }

        private static int GetMaxStackSize(IInventory inventory, IInventoryItem item)
        {
            var universal = inventory as UniversalInventory;
            if (universal == null)
                return int.MaxValue;

            return universal.GetMaxStackSizeForItem(item);
        }

        private static readonly IReadOnlyList<PlannedSlotAllocation> EmptyAllocations = new PlannedSlotAllocation[0];

        private static TransferPlan Fail(
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            ISlot targetSlotHint,
            TransferPlanFailureCode code,
            string reason,
            DragEntry? failedEntry = null)
        {
            return new TransferPlan(
                isValid: false,
                policy: policy,
                targetInventory: targetInventory,
                targetSlotHint: targetSlotHint,
                entries: new List<PlannedEntryTransfer>(),
                failure: new PlanFailure(code, reason, failedEntry));
        }

    }
}
