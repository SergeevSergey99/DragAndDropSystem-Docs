using System.Collections.Generic;
using System.Linq;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
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
        public PlannedSlotAllocation(BaseSlot baseSlot, int amount)
        {
            BaseSlot = baseSlot;
            Amount = amount;
        }

        public BaseSlot BaseSlot { get; }
        public int Amount { get; }
    }

    public sealed class PlannedSwapData
    {
        public PlannedSwapData(
            ItemStack sourceStackBefore,
            ItemStack targetStackBefore,
            ItemStack targetStackAfter,
            ItemStack sourceStackAfter)
        {
            SourceStackBefore = sourceStackBefore;
            TargetStackBefore = targetStackBefore;
            TargetStackAfter = targetStackAfter;
            SourceStackAfter = sourceStackAfter;
        }

        /// <summary>Clone of the source-slot stack before swap.</summary>
        public ItemStack SourceStackBefore { get; }
        /// <summary>Clone of the target-slot stack before swap.</summary>
        public ItemStack TargetStackBefore { get; }
        /// <summary>Source stack converted for the target inventory (what will be placed into target).</summary>
        public ItemStack TargetStackAfter { get; }
        /// <summary>Target stack converted for the source inventory (what will be placed into source).</summary>
        public ItemStack SourceStackAfter { get; }
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
            BaseSlot swapTargetBaseSlot = null,
            IItemAdapter previewTargetItemAdapter = null,
            bool requiresOccupiedHandler = false,
            BaseSlot occupiedTargetBaseSlot = null,
            PlannedSwapData swapData = null)
        {
            Entry = entry;
            RequestedAmount = requestedAmount;
            PlannedAmount = plannedAmount;
            Allocations = allocations;
            FailureReason = failureReason;
            RequiresSwap = requiresSwap;
            SwapTargetBaseSlot = swapTargetBaseSlot;
            PreviewTargetItemAdapter = previewTargetItemAdapter ?? entry.Stack?.PrimaryAdapter;
            RequiresOccupiedHandler = requiresOccupiedHandler;
            OccupiedTargetBaseSlot = occupiedTargetBaseSlot;
            SwapData = swapData;
        }

        public DragEntry Entry { get; }
        public int RequestedAmount { get; }
        public int PlannedAmount { get; }
        public IReadOnlyList<PlannedSlotAllocation> Allocations { get; }
        public string FailureReason { get; }
        public bool RequiresSwap { get; }
        public BaseSlot SwapTargetBaseSlot { get; }
        public IItemAdapter PreviewTargetItemAdapter { get; }
        public bool RequiresOccupiedHandler { get; }
        public BaseSlot OccupiedTargetBaseSlot { get; }
        public PlannedSwapData SwapData { get; }
        public bool IsPlanned => RequiresSwap || RequiresOccupiedHandler || (PlannedAmount > 0 && Allocations.Count > 0);
        public bool IsPartial => PlannedAmount > 0 && PlannedAmount < RequestedAmount;
    }

    public sealed class TransferPlan
    {
        public TransferPlan(
            bool isValid,
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            BaseSlot targetBaseSlotHint,
            IReadOnlyList<PlannedEntryTransfer> entries,
            PlanFailure failure = null)
        {
            IsValid = isValid;
            Policy = policy;
            TargetInventory = targetInventory;
            TargetBaseSlotHint = targetBaseSlotHint;
            Entries = entries ?? new List<PlannedEntryTransfer>();
            Failure = failure;
        }

        public bool IsValid { get; }
        public ResolvedDropPolicy Policy { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlotHint { get; }
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
    /// Transfer planner. Builds a plan without mutating inventory state.
    /// Plan execution is handled by a separate executor.
    /// </summary>
    public class TransferPlanner
    {
        private readonly RuleEvaluationService _ruleEvaluationService = new RuleEvaluationService();

        public TransferPlan BuildPlan(
            DragContext context,
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            BaseSlot targetBaseSlotHint,
            GlobalRuleValidator globalRules = null)
        {
            if (context == null || context.Entries == null || context.Entries.Count == 0)
            {
                return Fail(policy, targetInventory, targetBaseSlotHint, TransferPlanFailureCode.InvalidContext, "Drag context is empty");
            }

            if (targetInventory == null)
            {
                return Fail(policy, targetInventory, targetBaseSlotHint, TransferPlanFailureCode.InvalidTargetInventory, "Target inventory is null");
            }

            bool hasExistingSlots = targetInventory.Slots != null && targetInventory.Slots.Count > 0;
            bool canDeferSlotResolution = !hasExistingSlots && targetBaseSlotHint == null;
            if (!hasExistingSlots && !canDeferSlotResolution)
            {
                return Fail(policy, targetInventory, targetBaseSlotHint, TransferPlanFailureCode.NoTargetSlots, "Target inventory has no slots");
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
                            targetBaseSlotHint,
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
                    targetBaseSlotHint,
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
                            targetBaseSlotHint,
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
                        targetBaseSlotHint,
                        TransferPlanFailureCode.NotEnoughCapacity,
                        "Partial planning is not allowed by policy",
                        entry);
                }
            }

            var plan = new TransferPlan(
                isValid: true,
                policy: policy,
                targetInventory: targetInventory,
                targetBaseSlotHint: targetBaseSlotHint,
                entries: plannedEntries);

            if (!plan.HasAnyTransfer)
            {
                return Fail(
                    policy,
                    targetInventory,
                    targetBaseSlotHint,
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
            BaseSlot targetBaseSlotHint,
            bool isFirstEntry,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules)
        {
            if (entry.SourceInventory == null || entry.SourceBaseSlot == null || entry.Stack == null || entry.Stack.IsEmpty)
            {
                return new PlannedEntryTransfer(entry, 0, 0, EmptyAllocations, "Invalid source entry");
            }

            int requested = entry.Stack.Count;
            var sourceItem = entry.Stack.PrimaryAdapter;
            if (sourceItem == null || requested <= 0)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Invalid stack");
            }

            if (!TransferItemConversionUtility.TryResolveTargetItem(entry.SourceInventory, targetInventory, sourceItem, out var targetItem))
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Target inventory rejected itemAdapter conversion");
            }

            if (isFirstEntry &&
                targetBaseSlotHint != null &&
                !SupportsAlternativePlacement(policy))
            {
                return PlanHintOnlyEntry(
                    context,
                    entry,
                    policy,
                    targetInventory,
                    targetBaseSlotHint,
                    virtualSlots,
                    globalRules,
                    requested,
                    targetItem);
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
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Target inventory cannot accept this itemAdapter");
            }

            var operation = new EntryPlanningOperation(
                context,
                entry,
                policy,
                targetInventory,
                targetBaseSlotHint,
                preferHint: targetBaseSlotHint != null && isFirstEntry,
                virtualSlots,
                globalRules,
                targetItem,
                requested,
                acceptableByInventory);

            // Inventory-area drop (no target slot) into dynamic inventory can start with 0 slots.
            // In this case actual slot is resolved during execution via TryAddStack/TryAddToSlot.
            if ((operation.VirtualSlots == null || operation.VirtualSlots.Count == 0) && operation.TargetBaseSlotHint == null)
            {
                int deferredAmount = operation.Policy.AllowPartial
                    ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                    : operation.RequestedAmount;

                deferredAmount = ApplySourceStepToAmount(entry, deferredAmount);

                if (deferredAmount <= 0)
                    return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No capacity for deferred placement");

                return new PlannedEntryTransfer(
                    entry,
                    requested,
                    deferredAmount,
                    new[] { new PlannedSlotAllocation(null, deferredAmount) },
                    previewTargetItemAdapter: targetItem);
            }

            var allocations = IsUniqueInventory(operation.TargetInventory)
                ? AllocateForUniqueInventory(operation)
                : AllocateForStrategyInventory(operation);

            int plannedAmount = 0;
            foreach (var allocation in allocations)
                plannedAmount += allocation.Amount;

            // Round total amount down to a multiple of the source DragAmountStep.
            // Example: resultCount=6, capacity=64 -> transfer 60 (10 full crafts).
            plannedAmount = ApplySourceDragAmountStep(entry, plannedAmount, ref allocations);

            // Occupied slot handler takes priority: data binding may handle drop onto occupied slot
            // (e.g. placing item into a container). Must check before deferred allocation.
            if (plannedAmount == 0 && isFirstEntry && targetBaseSlotHint != null && !targetBaseSlotHint.IsEmpty
                && targetInventory is UniversalInventory occupiedUni
                && occupiedUni.CheckOccupiedSlotDrop(entry, targetBaseSlotHint))
            {
                return new PlannedEntryTransfer(
                    entry,
                    requested,
                    requested,
                    EmptyAllocations,
                    previewTargetItemAdapter: targetItem,
                    requiresOccupiedHandler: true,
                    occupiedTargetBaseSlot: targetBaseSlotHint);
            }

            // Dynamic inventories fallback: when virtual slot allocation found nothing
            // but the inventory reports capacity (via potentialNewSlots), defer to execution
            // which creates slots on the fly via TryAddStack / DynamicSlotDecorator.
            if (plannedAmount == 0 && operation.AcceptableByInventory > 0)
            {
                int deferredAmount = operation.Policy.AllowPartial
                    ? Min(operation.RequestedAmount, operation.AcceptableByInventory)
                    : operation.RequestedAmount;

                deferredAmount = ApplySourceStepToAmount(entry, deferredAmount);

                if (deferredAmount > 0)
                {
                    return new PlannedEntryTransfer(
                        entry,
                        requested,
                        deferredAmount,
                        new[] { new PlannedSlotAllocation(null, deferredAmount) },
                        previewTargetItemAdapter: targetItem);
                }
            }

            if (plannedAmount == 0)
            {
                var swapPlan = TryPlanSwap(operation.Context, operation.Entry, operation.Policy,
                    targetInventory, operation.TargetBaseSlotHint, operation.PreferHint, globalRules, requested, targetItem);
                if (swapPlan != null)
                    return swapPlan;

                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No valid slot found for entry");
            }

            if (plannedAmount < requested && !policy.AllowPartial)
            {
                return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Entry cannot be placed fully");
            }

            return new PlannedEntryTransfer(entry, requested, plannedAmount, allocations, previewTargetItemAdapter: targetItem);
        }

        private PlannedEntryTransfer PlanHintOnlyEntry(
            DragContext context,
            DragEntry entry,
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            BaseSlot targetBaseSlotHint,
            IReadOnlyList<VirtualSlotState> virtualSlots,
            GlobalRuleValidator globalRules,
            int requested,
            IItemAdapter targetItem)
        {
            var operation = new EntryPlanningOperation(
                context,
                entry,
                policy,
                targetInventory,
                targetBaseSlotHint,
                preferHint: true,
                virtualSlots,
                globalRules,
                targetItem,
                requested,
                requested);

            IReadOnlyList<PlannedSlotAllocation> allocations = EmptyAllocations;
            var hinted = FindVirtualSlot(targetBaseSlotHint, virtualSlots);
            if (hinted != null)
            {
                var hintedAllocations = new List<PlannedSlotAllocation>();
                int placedIntoHint = IsUniqueInventory(targetInventory)
                    ? TryAllocateIntoSlot(operation, hinted, 1, hintedAllocations, uniqueMode: true)
                    : TryAllocateIntoSlot(operation, hinted, requested, hintedAllocations, uniqueMode: false);

                if (placedIntoHint > 0)
                    allocations = hintedAllocations;
            }

            int plannedAmount = 0;
            foreach (var allocation in allocations)
                plannedAmount += allocation.Amount;

            plannedAmount = ApplySourceDragAmountStep(entry, plannedAmount, ref allocations);
            if (plannedAmount > 0)
            {
                if (plannedAmount < requested && !policy.AllowPartial)
                    return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "Entry cannot be placed fully");

                return new PlannedEntryTransfer(entry, requested, plannedAmount, allocations, previewTargetItemAdapter: targetItem);
            }

            if (!targetBaseSlotHint.IsEmpty &&
                targetInventory is UniversalInventory occupiedUni &&
                occupiedUni.CheckOccupiedSlotDrop(entry, targetBaseSlotHint))
            {
                return new PlannedEntryTransfer(
                    entry,
                    requested,
                    requested,
                    EmptyAllocations,
                    previewTargetItemAdapter: targetItem,
                    requiresOccupiedHandler: true,
                    occupiedTargetBaseSlot: targetBaseSlotHint);
            }

            var swapPlan = TryPlanSwap(context, entry, policy, targetInventory, targetBaseSlotHint, true, globalRules, requested, targetItem);
            if (swapPlan != null)
                return swapPlan;

            return new PlannedEntryTransfer(entry, requested, 0, EmptyAllocations, "No valid slot found for entry");
        }

        private static int ApplySourceStepToAmount(DragEntry entry, int amount)
        {
            if (amount <= 0 || entry.SourceInventory is not UniversalInventory sourceUni)
                return amount;
            int step = sourceUni.DragAmountStep;
            return step > 1 ? (amount / step) * step : amount;
        }

        /// <summary>
        /// If the source inventory has DragAmountStep > 1, rounds plannedAmount down
        /// to a multiple of step and trims allocations from the end.
        /// </summary>
        private static int ApplySourceDragAmountStep(
            DragEntry entry,
            int plannedAmount,
            ref IReadOnlyList<PlannedSlotAllocation> allocations)
        {
            if (plannedAmount <= 0)
                return plannedAmount;

            if (entry.SourceInventory is not UniversalInventory sourceUni)
                return plannedAmount;

            int step = sourceUni.DragAmountStep;
            if (step <= 1)
                return plannedAmount;

            int rounded = (plannedAmount / step) * step;
            if (rounded == plannedAmount)
                return plannedAmount;

            if (rounded <= 0)
            {
                allocations = EmptyAllocations;
                return 0;
            }

            int excess = plannedAmount - rounded;
            var trimmed = new List<PlannedSlotAllocation>(allocations.Count);
            for (int i = 0; i < allocations.Count; i++)
                trimmed.Add(allocations[i]);

            // Trim from the end
            for (int i = trimmed.Count - 1; i >= 0 && excess > 0; i--)
            {
                int cut = Min(trimmed[i].Amount, excess);
                int newAmount = trimmed[i].Amount - cut;
                excess -= cut;

                if (newAmount <= 0)
                    trimmed.RemoveAt(i);
                else
                    trimmed[i] = new PlannedSlotAllocation(trimmed[i].BaseSlot, newAmount);
            }

            allocations = trimmed;
            return rounded;
        }

        private static bool ShouldPlanSwap(
            DragContext context,
            DragEntry entry,
            ResolvedDropPolicy policy,
            BaseSlot targetBaseSlotHint,
            bool preferHint)
        {
            return SupportsSwap(policy);
        }

        private static bool SupportsSwap(ResolvedDropPolicy policy)
        {
            return policy.BlockedTargetResolver?.SwapStrategy != null;
        }

        private static bool SupportsAlternativePlacement(ResolvedDropPolicy policy)
        {
            return policy.BlockedTargetResolver?.AlternativePlacementStrategy != null;
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

            if (operation.PreferHint && operation.TargetBaseSlotHint != null)
            {
                var hinted = FindVirtualSlot(operation.TargetBaseSlotHint, operation.VirtualSlots);
                int placedIntoHint = TryAllocateIntoSlot(operation, hinted, remaining, allocations, uniqueMode: false);
                if (placedIntoHint > 0)
                {
                    anyPlaced = true;
                    remaining -= placedIntoHint;
                }

                if (remaining <= 0)
                    return allocations;

                if (!anyPlaced && SupportsSwap(operation.Policy))
                    return EmptyAllocations;

                if (!canSearchAlternatives)
                    return allocations;
            }

            if (remaining <= 0 || !canSearchAlternatives)
                return allocations;

            BaseSlot excludeFromAlternatives = operation.PreferHint ? operation.TargetBaseSlotHint : null;
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

            // For the first entry, try using the hinted slot as the preferred target.
            if (operation.PreferHint && operation.TargetBaseSlotHint != null)
            {
                var preferred = FindVirtualSlot(operation.TargetBaseSlotHint, operation.VirtualSlots);
                if (preferred != null &&
                    preferred.CanAccept(operation.TargetItemAdapter, uniqueMode: true) &&
                    IsCandidateAllowedByRules(operation, preferred.BaseSlot, 1))
                {
                    preferred.Apply(operation.TargetItemAdapter, 1);
                    allocations.Add(new PlannedSlotAllocation(preferred.BaseSlot, 1));
                }
                else if (!canSearchAlternatives)
                {
                    return EmptyAllocations;
                }
            }

            if (allocations.Count > 0 &&
                operation.PreferHint &&
                operation.TargetBaseSlotHint != null &&
                !canSearchAlternatives)
                return allocations;

            if (!canSearchAlternatives)
                return allocations;

            while (allocations.Count < desiredAmount)
            {
                var slot = FindNextAcceptingSlot(operation, 1, uniqueMode: true);
                if (slot == null)
                    break;

                slot.Apply(operation.TargetItemAdapter, 1);
                allocations.Add(new PlannedSlotAllocation(slot.BaseSlot, 1));
            }

            return allocations;
        }

        private static VirtualSlotState FindVirtualSlot(BaseSlot baseSlot, IReadOnlyList<VirtualSlotState> states)
        {
            if (baseSlot == null || states == null)
                return null;

            foreach (var state in states)
            {
                if (ReferenceEquals(state.BaseSlot, baseSlot))
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

            if (!IsCandidateAllowedByRules(operation, slot.BaseSlot, capacity))
                return 0;

            slot.Apply(operation.TargetItemAdapter, capacity);
            allocations.Add(new PlannedSlotAllocation(slot.BaseSlot, capacity));
            return capacity;
        }

        private int GetSlotPlacementCapacity(
            EntryPlanningOperation operation,
            VirtualSlotState slot,
            int desiredAmount,
            bool uniqueMode)
        {
            if (slot == null || desiredAmount <= 0 || operation.TargetItemAdapter == null)
                return 0;

            if (!CanStrategyPlaceIntoSlot(operation, slot, uniqueMode))
                return 0;

            if (uniqueMode)
                return 1;

            if (slot.IsEmpty)
            {
                return Min(desiredAmount, GetMaxStackSize(operation.TargetInventory, operation.TargetItemAdapter));
            }

            int maxStack = GetMaxStackSize(operation.TargetInventory, operation.TargetItemAdapter);
            if (maxStack <= slot.Count)
                return 0;

            return Min(desiredAmount, maxStack - slot.Count);
        }

        private static bool CanSearchAlternativeSlots(EntryPlanningOperation operation)
        {
            if (!SupportsAlternativePlacement(operation.Policy))
                return false;

            return !ReferenceEquals(operation.Entry.SourceInventory, operation.TargetInventory);
        }

        private bool CanStrategyPlaceIntoSlot(
            EntryPlanningOperation operation,
            VirtualSlotState slot,
            bool uniqueMode)
        {
            if (slot == null || operation.TargetItemAdapter == null)
                return false;

            if (uniqueMode)
                return slot.CanAccept(operation.TargetItemAdapter, uniqueMode: true);

            if (slot.IsEmpty)
                return slot.CanAccept(operation.TargetItemAdapter, uniqueMode: false);

            // Virtual slot is occupied: ensure itemAdapter compatibility at virtual level first.
            // Without this, CanUseAlternativeSlot checks the real (potentially empty) slot
            // and may incorrectly allow placing a different itemAdapter into a virtually occupied slot.
            if (!slot.CanAccept(operation.TargetItemAdapter, uniqueMode: false))
                return false;

            var placementStrategy = ResolvePlacementStrategy(operation.TargetInventory);
            if (placementStrategy == null)
                return true;

            return placementStrategy.CanUseAlternativeSlot(slot.BaseSlot, operation.TargetItemAdapter);
        }

        private IEnumerable<VirtualSlotState> EnumerateAlternativeVirtualSlots(EntryPlanningOperation operation, BaseSlot excludeBaseSlot)
        {
            if (operation.VirtualSlots == null || operation.VirtualSlots.Count == 0)
                yield break;

            var placementStrategy = ResolvePlacementStrategy(operation.TargetInventory);
            if (placementStrategy == null)
                yield break;

            var alternativePlacementStrategy = operation.Policy.BlockedTargetResolver?.AlternativePlacementStrategy;
            if (alternativePlacementStrategy == null)
                yield break;

            IEnumerable<BaseSlot> orderedSlots = alternativePlacementStrategy.EnumerateAlternativeSlots(
                GetInventorySlots(operation.TargetInventory),
                operation.TargetItemAdapter,
                excludeBaseSlot,
                placementStrategy.CanUseAlternativeSlot);

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
            BaseSlot preferredBaseSlot = null)
        {
            if (preferredBaseSlot != null)
            {
                foreach (var state in operation.VirtualSlots)
                {
                    if (ReferenceEquals(state.BaseSlot, preferredBaseSlot) &&
                        state.CanAccept(operation.TargetItemAdapter, uniqueMode) &&
                        IsCandidateAllowedByRules(operation, state.BaseSlot, amountForValidation))
                        return state;
                }
            }

            foreach (var state in operation.VirtualSlots)
            {
                if (state.CanAccept(operation.TargetItemAdapter, uniqueMode) &&
                    IsCandidateAllowedByRules(operation, state.BaseSlot, amountForValidation))
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

        private PlannedEntryTransfer TryPlanSwap(
            DragContext context,
            DragEntry entry,
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            BaseSlot targetBaseSlotHint,
            bool preferHint,
            GlobalRuleValidator globalRules,
            int requested,
            IItemAdapter targetItem)
        {
            if (!ShouldPlanSwap(context, entry, policy, targetBaseSlotHint, preferHint))
                return null;

            var swapStrategy = policy.BlockedTargetResolver?.SwapStrategy;
            if (swapStrategy == null)
                return null;

            var searchContext = new SwapSearchContext(context, entry, targetInventory, targetBaseSlotHint, preferHint);
            var swapTargets = swapStrategy.EnumerateSwapTargets(searchContext);
            if (swapTargets == null)
                return null;

            foreach (var swapTarget in swapTargets)
            {
                var planned = TryPlanSwapAgainstTarget(
                    context,
                    entry,
                    targetInventory,
                    swapTarget,
                    globalRules,
                    requested,
                    targetItem);

                if (planned != null)
                    return planned;
            }

            return null;
        }

        private PlannedEntryTransfer TryPlanSwapAgainstTarget(
            DragContext context,
            DragEntry entry,
            IInventory targetInventory,
            BaseSlot swapTargetBaseSlot,
            GlobalRuleValidator globalRules,
            int requested,
            IItemAdapter targetItem)
        {
            var sourceSlot = entry.SourceBaseSlot;
            if (sourceSlot == null || swapTargetBaseSlot == null)
                return null;
            if (ReferenceEquals(sourceSlot, swapTargetBaseSlot))
                return null;
            if (swapTargetBaseSlot.IsEmpty)
                return null;
            if (sourceSlot.IsEmpty || sourceSlot.Stack == null || sourceSlot.Stack.IsEmpty)
                return null;
            if (entry.Stack == null || entry.Stack.IsEmpty || entry.Stack.PrimaryAdapter == null)
                return null;
            // Current swap implementation supports only full stack from source slot.
            if (sourceSlot.Stack.Count != entry.Stack.Count)
                return null;

            if (!ItemStack.TryCreate(sourceSlot.Stack.Adapters, out var sourceStackBefore))
                return null;
            if (!ItemStack.TryCreate(swapTargetBaseSlot.Stack.Adapters, out var targetStackBefore))
                return null;

            if (!TransferItemConversionUtility.TryCreateConvertedStack(
                    entry.SourceInventory, targetInventory, sourceStackBefore, out var targetStackAfter))
                return null;
            if (!TransferItemConversionUtility.TryCreateConvertedStack(
                    targetInventory, entry.SourceInventory, targetStackBefore, out var sourceStackAfter))
                return null;

            // Validate reverse direction: can target items go back to source?
            var reverseContext = new DragContext(targetStackBefore, swapTargetBaseSlot, targetInventory, sourceSlot, entry.SourceInventory);
            var reverseEntry = reverseContext.Entries[0];
            var reverseStart = _ruleEvaluationService.ValidateEntryStart(reverseContext, reverseEntry, globalRules);
            if (!reverseStart.IsValid)
                return null;

            // Validate reverse drop: converted target items landing in source slot
            var reverseDropContext = new DragContext(sourceStackAfter, swapTargetBaseSlot, targetInventory, sourceSlot, entry.SourceInventory);
            var reverseDropEntry = reverseDropContext.Entries[0];
            var reverseDrop = _ruleEvaluationService.ValidateEntryDrop(reverseDropContext, reverseDropEntry, globalRules);
            if (!reverseDrop.IsValid)
                return null;

            // Validate forward drop: converted source items landing in target slot
            var sourceDropContext = new DragContext(targetStackAfter, sourceSlot, entry.SourceInventory, swapTargetBaseSlot, targetInventory);
            var sourceDropEntry = sourceDropContext.Entries[0];
            var sourceDrop = _ruleEvaluationService.ValidateEntryDrop(sourceDropContext, sourceDropEntry, globalRules);
            if (!sourceDrop.IsValid)
                return null;

            // Validate reverse placement capacity: can target items actually fit into source inventory?
            // Source slot will be freed by the swap, so mark it empty in virtual state.
            if (!ValidateSwapPlacementFeasibility(
                    reverseDropContext, reverseDropEntry, entry.SourceInventory, sourceSlot,
                    sourceStackAfter, globalRules))
                return null;

            // Validate forward placement capacity: can source items fit into target inventory?
            // Target slot will be freed by the swap.
            if (!ValidateSwapPlacementFeasibility(
                    sourceDropContext, sourceDropEntry, targetInventory, swapTargetBaseSlot,
                    targetStackAfter, globalRules))
                return null;

            var swapData = new PlannedSwapData(sourceStackBefore, targetStackBefore, targetStackAfter, sourceStackAfter);

            return new PlannedEntryTransfer(
                entry,
                requested,
                requested,
                EmptyAllocations,
                failureReason: null,
                requiresSwap: true,
                swapTargetBaseSlot: swapTargetBaseSlot,
                previewTargetItemAdapter: targetItem,
                swapData: swapData);
        }

        /// <summary>
        /// Checks whether a stack can be placed into the inventory during a swap.
        /// freedSlot will be freed by the swap, so it is marked as empty in virtual state.
        /// </summary>
        private bool ValidateSwapPlacementFeasibility(
            DragContext dropContext,
            DragEntry dropEntry,
            IInventory inventory,
            BaseSlot freedBaseSlot,
            ItemStack convertedStack,
            GlobalRuleValidator globalRules)
        {
            if (convertedStack == null || convertedStack.IsEmpty)
                return false;

            // Single item always fits into the freed slot (rules already validated above).
            if (convertedStack.Count <= 1)
                return true;

            var virtualSlots = BuildVirtualSlots(inventory);
            var freed = FindVirtualSlot(freedBaseSlot, virtualSlots);
            if (freed != null)
                freed.MarkEmpty();

            bool isUnique = IsUniqueInventory(inventory);
            var placementResolver = new FindAlternativeBlockedTargetResolver();
            placementResolver.SetAlternativePlacementStrategy(new EmptyFirstAlternativePlacementStrategy());
            var placementPolicy = new ResolvedDropPolicy(
                placementResolver,
                allowPartial: false,
                BatchMode.BestEffort);

            var operation = new EntryPlanningOperation(
                dropContext,
                dropEntry,
                placementPolicy,
                inventory,
                freedBaseSlot,
                preferHint: true,
                virtualSlots,
                globalRules,
                convertedStack.PrimaryAdapter,
                convertedStack.Count,
                convertedStack.Count);

            var allocations = isUnique
                ? AllocateForUniqueInventory(operation)
                : AllocateForStrategyInventory(operation);

            int allocated = 0;
            foreach (var a in allocations)
                allocated += a.Amount;

            return allocated >= convertedStack.Count;
        }

        private bool IsCandidateAllowedByRules(
            EntryPlanningOperation operation,
            BaseSlot targetBaseSlot,
            int plannedAmount)
        {
            if (plannedAmount <= 0 || operation.TargetItemAdapter == null)
                return false;

            DragEntry validationEntry = operation.Entry;
            if (operation.Entry.Stack == null ||
                operation.Entry.Stack.PrimaryAdapter == null ||
                operation.Entry.Stack.Count != plannedAmount ||
                !ReferenceEquals(operation.Entry.Stack.PrimaryAdapter, operation.TargetItemAdapter))
            {
                var validationRequest = new InventoryAcceptanceRequest(
                    operation.TargetInventory,
                    operation.TargetItemAdapter,
                    plannedAmount,
                    operation.Context,
                    operation.Entry);
                var validationStack = validationRequest.CreatePreviewStack(plannedAmount, operation.TargetItemAdapter);
                if (validationStack == null)
                    return false;
                validationEntry = new DragEntry(
                    validationStack,
                    operation.Entry.SourceBaseSlot,
                    operation.Entry.SourceInventory);
            }

            var result = _ruleEvaluationService.ValidateEntryDrop(
                operation.Context.WithTarget(targetBaseSlot, operation.TargetInventory),
                validationEntry,
                operation.GlobalRules);
            return result.IsValid;
        }

        private static IPlacementStrategy ResolvePlacementStrategy(IInventory inventory)
        {
            var universal = inventory as UniversalInventory;
            return universal != null ? universal.PlacementStrategy : null;
        }

        private static List<BaseSlot> GetInventorySlots(IInventory inventory)
        {
            if (inventory == null || inventory.Slots == null)
                return null;

            return inventory.Slots as List<BaseSlot> ?? new List<BaseSlot>(inventory.Slots);
        }

        private static int GetMaxStackSize(IInventory inventory, IItemAdapter itemAdapter)
        {
            var universal = inventory as UniversalInventory;
            if (universal == null)
                return int.MaxValue;

            return universal.GetMaxStackSizeForItem(itemAdapter);
        }

        private static readonly IReadOnlyList<PlannedSlotAllocation> EmptyAllocations = new PlannedSlotAllocation[0];

        private static TransferPlan Fail(
            ResolvedDropPolicy policy,
            IInventory targetInventory,
            BaseSlot targetBaseSlotHint,
            TransferPlanFailureCode code,
            string reason,
            DragEntry? failedEntry = null)
        {
            return new TransferPlan(
                isValid: false,
                policy: policy,
                targetInventory: targetInventory,
                targetBaseSlotHint: targetBaseSlotHint,
                entries: new List<PlannedEntryTransfer>(),
                failure: new PlanFailure(code, reason, failedEntry));
        }

    }
}
