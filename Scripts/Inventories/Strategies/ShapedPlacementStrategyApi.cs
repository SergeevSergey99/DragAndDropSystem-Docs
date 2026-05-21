using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Inputs that a strategy needs to plan a shaped (multi-cell) placement
    /// for a single drag entry against a target inventory.
    /// </summary>
    public readonly struct ShapedPlacementPlanContext
    {
        public ShapedPlacementPlanContext(
            DragContext dragContext,
            DragEntry entry,
            IInventory targetInventory,
            BaseSlot targetBaseSlotHint,
            IItemAdapter targetItemAdapter,
            Footprint footprint,
            int requestedAmount,
            PlacementOrientation orientation)
        {
            DragContext = dragContext;
            Entry = entry;
            TargetInventory = targetInventory;
            TargetBaseSlotHint = targetBaseSlotHint;
            TargetItemAdapter = targetItemAdapter;
            Footprint = footprint;
            RequestedAmount = requestedAmount;
            Orientation = orientation;
        }

        public DragContext DragContext { get; }
        public DragEntry Entry { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlotHint { get; }
        public IItemAdapter TargetItemAdapter { get; }
        public Footprint Footprint { get; }
        public int RequestedAmount { get; }
        public PlacementOrientation Orientation { get; }
    }

    public enum ShapedPlacementPlanOutcome
    {
        /// <summary>
        /// The strategy doesn't apply to this case. Caller should fall back to its default planning path.
        /// </summary>
        NotApplicable = 0,

        /// <summary>
        /// The strategy planned a placement. Allocation field is valid.
        /// </summary>
        Planned = 1,

        /// <summary>
        /// The strategy refused this placement. FailureReason carries the explanation.
        /// </summary>
        Rejected = 2
    }

    public readonly struct ShapedPlacementPlanResult
    {
        private ShapedPlacementPlanResult(
            ShapedPlacementPlanOutcome outcome,
            PlannedPlacementAllocation allocation,
            string failureReason)
        {
            Outcome = outcome;
            Allocation = allocation;
            FailureReason = failureReason;
        }

        public ShapedPlacementPlanOutcome Outcome { get; }
        public PlannedPlacementAllocation Allocation { get; }
        public string FailureReason { get; }

        public static ShapedPlacementPlanResult NotApplicable() =>
            new ShapedPlacementPlanResult(ShapedPlacementPlanOutcome.NotApplicable, default, null);

        public static ShapedPlacementPlanResult Planned(PlannedPlacementAllocation allocation) =>
            new ShapedPlacementPlanResult(ShapedPlacementPlanOutcome.Planned, allocation, null);

        public static ShapedPlacementPlanResult Rejected(string reason) =>
            new ShapedPlacementPlanResult(ShapedPlacementPlanOutcome.Rejected, default, reason);
    }

    /// <summary>
    /// Inputs that a strategy needs to apply a previously planned shaped placement
    /// to a target inventory during execution.
    /// </summary>
    public readonly struct ShapedPlacementExecutionContext
    {
        public ShapedPlacementExecutionContext(
            IInventory targetInventory,
            ItemStack transferStack,
            int transferAmount,
            PlannedPlacementAllocation allocation,
            PlacementOrientation orientation,
            SlotOperationContext operationContext)
        {
            TargetInventory = targetInventory;
            TransferStack = transferStack;
            TransferAmount = transferAmount;
            Allocation = allocation;
            Orientation = orientation;
            OperationContext = operationContext;
        }

        public IInventory TargetInventory { get; }

        /// <summary>
        /// Mutable: items consumed by the strategy must be removed from this stack.
        /// </summary>
        public ItemStack TransferStack { get; }
        public int TransferAmount { get; }
        public PlannedPlacementAllocation Allocation { get; }
        public PlacementOrientation Orientation { get; }
        public SlotOperationContext OperationContext { get; }
    }

    public enum ShapedPlacementExecutionOutcome
    {
        /// <summary>
        /// Strategy didn't apply to this allocation. Caller should fall back.
        /// </summary>
        NotApplicable = 0,

        /// <summary>
        /// Placement applied. ResolvedAnchorSlot/PlacedAmount are valid.
        /// </summary>
        Placed = 1,

        /// <summary>
        /// Strategy attempted but failed to commit the placement.
        /// </summary>
        Failed = 2
    }

    public readonly struct ShapedPlacementExecutionResult
    {
        private ShapedPlacementExecutionResult(
            ShapedPlacementExecutionOutcome outcome,
            BaseSlot resolvedAnchorSlot,
            bool targetWasEmpty,
            int placedAmount,
            string failureReason)
        {
            Outcome = outcome;
            ResolvedAnchorSlot = resolvedAnchorSlot;
            TargetWasEmpty = targetWasEmpty;
            PlacedAmount = placedAmount;
            FailureReason = failureReason;
        }

        public ShapedPlacementExecutionOutcome Outcome { get; }
        public BaseSlot ResolvedAnchorSlot { get; }
        public bool TargetWasEmpty { get; }
        public int PlacedAmount { get; }
        public string FailureReason { get; }

        public static ShapedPlacementExecutionResult NotApplicable() =>
            new ShapedPlacementExecutionResult(ShapedPlacementExecutionOutcome.NotApplicable, null, false, 0, null);

        public static ShapedPlacementExecutionResult Placed(BaseSlot anchorSlot, bool wasEmpty, int placedAmount) =>
            new ShapedPlacementExecutionResult(ShapedPlacementExecutionOutcome.Placed, anchorSlot, wasEmpty, placedAmount, null);

        public static ShapedPlacementExecutionResult Failed(string reason) =>
            new ShapedPlacementExecutionResult(ShapedPlacementExecutionOutcome.Failed, null, false, 0, reason);
    }
}
