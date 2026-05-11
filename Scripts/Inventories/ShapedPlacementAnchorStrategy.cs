using System;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    public readonly struct ShapedPlacementAnchorContext
    {
        public ShapedPlacementAnchorContext(
            IPlacementInventory targetInventory,
            BaseSlot targetBaseSlot,
            DragContext dragContext,
            DragEntry entry,
            Footprint footprint,
            PlacementOrientation orientation,
            IItemAdapter targetItemAdapter)
        {
            TargetInventory = targetInventory;
            TargetBaseSlot = targetBaseSlot;
            DragContext = dragContext;
            Entry = entry;
            Footprint = footprint;
            Orientation = orientation;
            TargetItemAdapter = targetItemAdapter;
        }

        public IPlacementInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlot { get; }
        public DragContext DragContext { get; }
        public DragEntry Entry { get; }
        public Footprint Footprint { get; }
        public PlacementOrientation Orientation { get; }
        public IItemAdapter TargetItemAdapter { get; }

        public Vector2Int TargetCell => TargetInventory != null && TargetBaseSlot != null
            ? TargetInventory.GetCellForIndex(TargetBaseSlot.Index)
            : Vector2Int.zero;

        public Vector2Int SourceGrabOffset
        {
            get
            {
                if (Entry.SourcePlacement != null &&
                    Entry.SourceBaseSlot != null &&
                    Entry.SourceInventory is IPlacementInventory sourceInventory)
                {
                    return sourceInventory.GetGrabOffset(Entry.SourcePlacement, Entry.SourceBaseSlot);
                }

                return Entry.GrabOffset;
            }
        }
    }

    public interface IShapedPlacementAnchorStrategy
    {
        bool TryResolveAnchorCell(ShapedPlacementAnchorContext context, out Vector2Int anchorCell);
    }

    [Serializable]
    public abstract class ShapedPlacementAnchorStrategyBase : IShapedPlacementAnchorStrategy
    {
        public abstract bool TryResolveAnchorCell(ShapedPlacementAnchorContext context, out Vector2Int anchorCell);
    }

    [Serializable]
    public sealed class RotatedGrabOffsetAnchorStrategy : ShapedPlacementAnchorStrategyBase
    {
        public override bool TryResolveAnchorCell(ShapedPlacementAnchorContext context, out Vector2Int anchorCell)
        {
            if (context.TargetInventory == null || context.TargetBaseSlot == null)
            {
                anchorCell = Vector2Int.zero;
                return false;
            }

            anchorCell = context.TargetCell - context.Entry.GrabOffset;
            return true;
        }
    }

    [Serializable]
    public sealed class SourceGrabOffsetAnchorStrategy : ShapedPlacementAnchorStrategyBase
    {
        public override bool TryResolveAnchorCell(ShapedPlacementAnchorContext context, out Vector2Int anchorCell)
        {
            if (context.TargetInventory == null || context.TargetBaseSlot == null)
            {
                anchorCell = Vector2Int.zero;
                return false;
            }

            anchorCell = context.TargetCell - context.SourceGrabOffset;
            return true;
        }
    }

    [Serializable]
    public sealed class TargetSlotAnchorStrategy : ShapedPlacementAnchorStrategyBase
    {
        public override bool TryResolveAnchorCell(ShapedPlacementAnchorContext context, out Vector2Int anchorCell)
        {
            if (context.TargetInventory == null || context.TargetBaseSlot == null)
            {
                anchorCell = Vector2Int.zero;
                return false;
            }

            anchorCell = context.TargetCell;
            return true;
        }
    }
}
