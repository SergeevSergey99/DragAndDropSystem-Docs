using System;
using System.Collections.Generic;
using UDND.Core;
using UnityEngine;

namespace UDND.Inventories
{
    public readonly struct PlacementStoreSettings
    {
        public PlacementStoreSettings(
            IInventoryTopology topology,
            SlotShapedItemPolicy slotShapedItemPolicy = SlotShapedItemPolicy.Accept)
        {
            Topology = topology ?? new SlotTopology(0);
            SlotShapedItemPolicy = slotShapedItemPolicy;
        }

        public IInventoryTopology Topology { get; }
        public SlotShapedItemPolicy SlotShapedItemPolicy { get; }
    }

    public sealed class PlacementStore
    {
        private static readonly IReadOnlyList<int> EmptyIndices = Array.Empty<int>();

        private readonly Dictionary<int, Placement> _cellToPlacement = new Dictionary<int, Placement>();
        private readonly HashSet<Placement> _placements = new HashSet<Placement>();
        private readonly PlacementStoreSettings _settings;

        public PlacementStore(PlacementStoreSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyCollection<Placement> Placements => _placements;
        public IInventoryTopology Topology => _settings.Topology;
        public SlotShapedItemPolicy SlotShapedItemPolicy => _settings.SlotShapedItemPolicy;

        public void Reset()
        {
            _cellToPlacement.Clear();
            _placements.Clear();
        }

        public bool CanPlace(PlacementRequest request, Placement ignoredPlacement = null)
        {
            if (request.Stack == null || request.Stack.IsEmpty)
                return false;

            if (IsRejectedShapedSlotPlacement(request.Shape, request.Orientation))
                return false;

            if (!PlacementShapeUtility.IsSingleCell(request.Shape, request.Orientation) && request.Stack.Count > 1)
                return false;

            var coveredIndices = GetCoveredIndices(
                request.AnchorIndex,
                request.Shape,
                request.Orientation,
                PlacementBoundsMode.RequireAllInBounds);
            if (coveredIndices == null || coveredIndices.Count == 0)
                return false;

            for (int i = 0; i < coveredIndices.Count; i++)
            {
                if (_cellToPlacement.TryGetValue(coveredIndices[i], out var existing) &&
                    !ReferenceEquals(existing, ignoredPlacement))
                    return false;
            }

            return true;
        }

        public bool TryPlace(PlacementRequest request, out Placement placement)
        {
            placement = null;
            if (!CanPlace(request))
                return false;

            var coveredIndices = GetCoveredIndices(
                request.AnchorIndex,
                request.Shape,
                request.Orientation,
                PlacementBoundsMode.RequireAllInBounds);
            if (coveredIndices == null || coveredIndices.Count == 0)
                return false;

            placement = new Placement(
                Topology.ToCell(request.AnchorIndex),
                request.AnchorIndex,
                request.Orientation,
                request.Shape,
                request.Stack,
                coveredIndices);

            Register(placement);
            return true;
        }

        public Placement GetAt(int cellIndex)
            => _cellToPlacement.TryGetValue(cellIndex, out var placement) ? placement : null;

        public bool Remove(Placement placement)
            => Unregister(placement);

        public bool RemoveAt(int cellIndex)
        {
            var placement = GetAt(cellIndex);
            return placement != null && Unregister(placement);
        }

        public IReadOnlyList<int> GetCoveredIndices(
            int anchorIndex,
            IPlacementShape shape,
            PlacementOrientation orientation,
            PlacementBoundsMode boundsMode)
        {
            if (!Topology.IsValidIndex(anchorIndex))
                return EmptyIndices;

            if (IsRejectedShapedSlotPlacement(shape, orientation))
                return EmptyIndices;

            if (ShouldCollapseToAnchor(shape, orientation))
                return new[] { anchorIndex };

            return PlacementCellUtility.GetCoveredIndices(
                anchorIndex,
                shape,
                orientation,
                Topology,
                boundsMode);
        }

        public IReadOnlyList<int> GetCoveredIndices(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            PlacementBoundsMode boundsMode)
        {
            if (IsRejectedShapedSlotPlacement(shape, orientation))
                return EmptyIndices;

            if (ShouldCollapseToAnchor(shape, orientation))
            {
                return Topology.TryToIndex(anchorCell, out int anchorIndex)
                    ? new[] { anchorIndex }
                    : EmptyIndices;
            }

            return PlacementCellUtility.GetCoveredIndices(
                anchorCell,
                shape,
                orientation,
                Topology,
                boundsMode);
        }

        public void ShiftAfterSlotRemoved(int removedIndex)
        {
            if (removedIndex < 0)
                return;

            var shiftedPlacements = new List<Placement>(_placements.Count);
            foreach (var placement in _placements)
            {
                if (placement == null || placement.MutableStack == null || placement.MutableStack.IsEmpty)
                    continue;

                int anchorIndex = placement.AnchorIndex > removedIndex
                    ? placement.AnchorIndex - 1
                    : placement.AnchorIndex;
                var covered = GetCoveredIndices(
                    anchorIndex,
                    placement.Shape,
                    placement.Orientation,
                    PlacementBoundsMode.RequireAllInBounds);
                if (covered == null || covered.Count == 0)
                    continue;

                shiftedPlacements.Add(new Placement(
                    Topology.ToCell(anchorIndex),
                    anchorIndex,
                    placement.Orientation,
                    placement.Shape,
                    placement.MutableStack,
                    covered));
            }

            Reset();
            for (int i = 0; i < shiftedPlacements.Count; i++)
                Register(shiftedPlacements[i]);
        }

        private void Register(Placement placement)
        {
            if (placement == null)
                return;

            _placements.Add(placement);
            for (int i = 0; i < placement.CoveredIndices.Count; i++)
                _cellToPlacement[placement.CoveredIndices[i]] = placement;
        }

        private bool Unregister(Placement placement)
        {
            if (placement == null)
                return false;

            bool removed = false;
            for (int i = 0; i < placement.CoveredIndices.Count; i++)
            {
                int cellIndex = placement.CoveredIndices[i];
                if (_cellToPlacement.TryGetValue(cellIndex, out var existing) &&
                    ReferenceEquals(existing, placement))
                {
                    _cellToPlacement.Remove(cellIndex);
                    removed = true;
                }
            }

            _placements.Remove(placement);
            return removed;
        }

        private bool IsRejectedShapedSlotPlacement(IPlacementShape shape, PlacementOrientation orientation)
            => Topology is SlotTopology &&
               SlotShapedItemPolicy == SlotShapedItemPolicy.Reject &&
               !PlacementShapeUtility.IsSingleCell(shape, orientation);

        private bool ShouldCollapseToAnchor(IPlacementShape shape, PlacementOrientation orientation)
            => Topology is SlotTopology &&
               SlotShapedItemPolicy == SlotShapedItemPolicy.Accept &&
               !PlacementShapeUtility.IsSingleCell(shape, orientation);
    }
}
