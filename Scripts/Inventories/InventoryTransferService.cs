using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public readonly struct InventoryTransferRequest
    {
        public InventoryTransferRequest(
            IInventory sourceInventory,
            BaseSlot sourceBaseSlot,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ItemStack draggedStack,
            PlacementOrientation orientation = PlacementOrientation.Rot0)
        {
            SourceInventory = sourceInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetInventory = targetInventory;
            TargetBaseSlot = targetBaseSlot;
            DraggedStack = draggedStack;
            Orientation = orientation;
        }

        public IInventory SourceInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlot { get; }
        public ItemStack DraggedStack { get; }
        public PlacementOrientation Orientation { get; }

        public bool IsValid =>
            SourceInventory != null &&
            SourceBaseSlot != null &&
            TargetInventory != null &&
            DraggedStack != null &&
            DraggedStack.PrimaryAdapter != null &&
            DraggedStack.Count > 0;
    }

    public readonly struct InventoryTransferResult
    {
        public InventoryTransferResult(
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            ItemStack sourceRemovedStack,
            ItemStack transferredStack,
            bool targetWasEmptyBefore,
            int remainingInSource = 0,
            PlacementSnapshot targetPlacementSnapshot = null,
            PlacementSnapshot sourcePlacementSnapshot = null)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            SourceRemovedStack = sourceRemovedStack ?? ItemStack.Empty();
            TransferredStack = transferredStack ?? ItemStack.Empty();
            TargetWasEmptyBefore = targetWasEmptyBefore;
            RemainingInSource = remainingInSource;
            TargetPlacementSnapshot = targetPlacementSnapshot;
            SourcePlacementSnapshot = sourcePlacementSnapshot;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }

        /// <summary>
        /// Stack of adapters removed from the source (before conversion)
        /// </summary>
        public ItemStack SourceRemovedStack { get; }

        /// <summary>
        /// Stack of adapters added to the target (after conversion)
        /// </summary>
        public ItemStack TransferredStack { get; }

        public IItemAdapter SourceItemAdapter => SourceRemovedStack.PrimaryAdapter;
        public IItemAdapter TargetItemAdapter => TransferredStack.PrimaryAdapter;
        public IItemAdapter ItemAdapter => TargetItemAdapter;
        public int Amount => TransferredStack.Count;
        public bool TargetWasEmptyBefore { get; }
        public int RemainingInSource { get; }
        public bool IsPartialTransfer => RemainingInSource > 0;
        public PlacementSnapshot SourcePlacementSnapshot { get; }
        public PlacementSnapshot TargetPlacementSnapshot { get; }
        public PlacementSnapshot PlacementSnapshot => TargetPlacementSnapshot;
        public BaseSlot AnchorSlot => TargetPlacementSnapshot?.AnchorBaseSlot ?? TargetBaseSlot;
        public IReadOnlyList<BaseSlot> CoveredSlots => TargetPlacementSnapshot?.CoveredBaseSlots ?? Array.Empty<BaseSlot>();
        public IReadOnlyList<int> CoveredIndices => TargetPlacementSnapshot?.CoveredIndices ?? Array.Empty<int>();
        public int AnchorIndex => TargetPlacementSnapshot != null && TargetPlacementSnapshot.AnchorIndex >= 0
            ? TargetPlacementSnapshot.AnchorIndex
            : TargetBaseSlot?.Index ?? -1;
        public PlacementOrientation Orientation => TargetPlacementSnapshot?.Orientation ?? PlacementOrientation.Rot0;
        public Footprint Footprint => TargetPlacementSnapshot?.Footprint ?? Footprint.One;
    }
}
