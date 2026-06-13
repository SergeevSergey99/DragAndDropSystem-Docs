using System.Collections.Generic;
using UnityEngine;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Core
{
    /// <summary>
    /// One entry in a drag operation (source + stack)
    /// </summary>
    public readonly struct DragEntry
    {
        public ItemStack Stack { get; }
        public BaseSlot SourceBaseSlot { get; }
        public IInventory SourceInventory { get; }
        public Placement SourcePlacement { get; }
        public Vector2Int GrabOffset { get; }
        public IPlacementShape Shape { get; }
        public Vector2Int BoundingSize { get; }
        public int Orientation { get; }
        public IInventoryTopology OrientationTopology { get; }
        public bool IsShaped => !PlacementShapeUtility.IsSingleCell(
            Shape,
            Orientation,
            OrientationTopology);

        public DragEntry(
            ItemStack stack,
            BaseSlot sourceBaseSlot,
            IInventory sourceInventory,
            Placement sourcePlacement = null,
            Vector2Int? grabOffset = null,
            int? orientation = null,
            IInventoryTopology orientationTopology = null)
        {
            Stack = stack;
            SourceBaseSlot = sourceBaseSlot;
            SourceInventory = sourceInventory;
            SourcePlacement = sourcePlacement;

            var sourceStore = sourceInventory ?? sourceBaseSlot?.Inventory;
            if (SourcePlacement == null && sourceStore != null && sourceBaseSlot != null &&
                sourceStore.TryGetPlacementAt(sourceBaseSlot, out var resolvedPlacement))
                SourcePlacement = resolvedPlacement;

            Shape = SourcePlacement?.Shape ?? PlacementShapeUtility.Resolve(stack?.PrimaryAdapter);
            OrientationTopology = orientationTopology ?? ResolveOrientationTopology(sourceStore);
            var sourceOrientation = OrientationTopology.NormalizeOrientation(
                SourcePlacement?.Orientation ?? 0);
            Orientation = OrientationTopology.NormalizeOrientation(orientation ?? sourceOrientation);
            BoundingSize = PlacementShapeUtility.GetBoundingSize(
                Shape,
                Orientation,
                OrientationTopology);

            var resolvedGrabOffset = grabOffset
                ?? (sourceStore != null
                    ? sourceStore.GetGrabOffset(SourcePlacement, sourceBaseSlot)
                    : Vector2Int.zero);
            GrabOffset = grabOffset.HasValue
                ? resolvedGrabOffset
                : OrientationTopology.RotateOffset(
                    resolvedGrabOffset,
                    Shape,
                    sourceOrientation,
                    Orientation);
        }

        public DragEntry WithOrientation(
            int orientation,
            IInventoryTopology orientationTopology = null)
        {
            var topology = orientationTopology ?? OrientationTopology;
            var normalizedOrientation = topology.NormalizeOrientation(orientation);
            var baseGrabOffset = OrientationTopology.RotateOffset(
                GrabOffset,
                Shape,
                Orientation,
                0);
            return new DragEntry(
                Stack,
                SourceBaseSlot,
                SourceInventory,
                SourcePlacement,
                topology.RotateOffset(
                    baseGrabOffset,
                    Shape,
                    0,
                    normalizedOrientation),
                normalizedOrientation,
                topology);
        }

        private static IInventoryTopology ResolveOrientationTopology(IInventory inventory)
        {
            if (inventory is IPlacementInventory placementInventory &&
                placementInventory.Topology != null &&
                placementInventory.Topology.OrientationCount > 1)
            {
                return placementInventory.Topology;
            }

            return new RectGridTopology(1, 1);
        }
    }

    /// <summary>
    /// Drag operation context
    /// Supports both single and multi-entry drag (batch)
    /// </summary>
    public class DragContext
    {
        public IReadOnlyList<DragEntry> Entries { get; }
        public bool IsBatchDrag => Entries.Count > 1;
        public bool HasShapedEntries
        {
            get
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].IsShaped)
                        return true;
                }

                return false;
            }
        }

        public bool HasStackedShapedEntries
        {
            get
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    var entry = Entries[i];
                    if (entry.IsShaped && entry.Stack != null && entry.Stack.Count > 1)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Target slot of the operation.
        /// <para>
        /// <b>null</b> means the target is not specified (auto-transfer, drop onto an inventory area, code-driven call).<br/>
        /// <b>Single drag:</b> exact final slot, slot rules are applied directly.<br/>
        /// <b>Batch drag:</b> UI hint (slot under the cursor). The final slot for each entry is unknown
        /// until the actual transfer and is determined by the JIT transfer service.
        /// Rules should use <see cref="IsBatchDrag"/> to ignore TargetSlot during batch validation.
        /// </para>
        /// </summary>
        public BaseSlot TargetBaseSlot { get; set; }

        public IInventory TargetInventory { get; set; }

        /// <summary>
        /// True if we have any target (slot or inventory).
        /// For world drops, both may be null - use processor-based validation instead.
        /// </summary>
        public bool HasTarget => TargetBaseSlot != null || TargetInventory != null;

        /// <summary>
        /// True if we have a specific target slot
        /// </summary>
        public bool HasTargetSlot => TargetBaseSlot != null;

        /// <summary>
        /// True if we have a target inventory
        /// </summary>
        public bool HasTargetInventory => TargetInventory != null;

        /// <summary>
        /// Constructor for a single entry (main scenario)
        /// </summary>
        public DragContext(ItemStack stack, BaseSlot sourceBaseSlot, IInventory sourceInventory)
        {
            Entries = new[] { new DragEntry(stack, sourceBaseSlot, sourceInventory) };
        }
        /// <summary>
        /// Constructor for a single entry with a target (for example, a code call with a pre-known target)
        /// </summary>
        public DragContext(ItemStack stack, BaseSlot sourceBaseSlot, IInventory sourceInventory, BaseSlot targetBaseSlot, IInventory targetInventory)
        {
            Entries = new[] { new DragEntry(stack, sourceBaseSlot, sourceInventory) };
            SetTarget(targetBaseSlot, targetInventory);
        }

        /// <summary>
        /// Constructor for multiple entries (batch drag)
        /// </summary>
        public DragContext(IReadOnlyList<DragEntry> entries)
        {
            Entries = entries;
        }

        private DragContext(IReadOnlyList<DragEntry> entries, BaseSlot targetBaseSlot, IInventory targetInventory)
        {
            Entries = entries;
            TargetBaseSlot = targetBaseSlot;
            TargetInventory = targetInventory;
        }

        /// <summary>
        /// Creates a copy of the context with a specified target for rule validation.
        /// The original context is not modified.
        /// </summary>
        public DragContext WithTarget(BaseSlot targetBaseSlot, IInventory targetInventory)
            => new DragContext(Entries, targetBaseSlot, targetInventory);

        public DragContext WithEntries(IReadOnlyList<DragEntry> entries)
            => new DragContext(entries, TargetBaseSlot, TargetInventory);

        public void SetTarget(BaseSlot targetBaseSlot, IInventory targetInventory)
        {
            TargetBaseSlot = targetBaseSlot;
            TargetInventory = targetInventory;
        }

        public void ClearTarget()
        {
            TargetBaseSlot = null;
            TargetInventory = null;
        }
    }
}
