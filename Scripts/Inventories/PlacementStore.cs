using System;
using System.Collections.Generic;
using UDND.Core;
using UnityEngine;

namespace UDND.Inventories
{
    public sealed class PlacementStore
    {
        private static readonly IReadOnlyList<int> EmptyIndices = Array.Empty<int>();

        private readonly Dictionary<int, Placement> _cellToPlacement = new Dictionary<int, Placement>();
        private readonly HashSet<Placement> _placements = new HashSet<Placement>();
        private readonly IInventoryTopology _topology;

        public PlacementStore(IInventoryTopology topology)
        {
            _topology = topology ?? new SlotTopology(0);
        }

        public IReadOnlyCollection<Placement> Placements => _placements;
        public IInventoryTopology Topology => _topology;

        public void Reset()
        {
            _cellToPlacement.Clear();
            _placements.Clear();
        }

        public bool CanPlace(PlacementRequest request, Placement ignoredPlacement = null)
            => CanPlace(request, ignoredPlacement, null);

        /// <summary>
        /// Geometry feasibility ignoring up to two placements. Both-ignored is needed by swaps:
        /// a same-inventory swap vacates both the source and the target placement before the
        /// incoming footprints are tested, so overlapping footprints must not block each other.
        /// </summary>
        public bool CanPlace(PlacementRequest request, Placement ignoredA, Placement ignoredB)
        {
            if (request.Stack == null || request.Stack.IsEmpty)
                return false;

            // Geometry only: a placement is just a footprint over covered cells. Stack quantity
            // (count == 1 vs count > 1) is governed by the strategy (GetMaxStackSize) and the
            // transfer strategy, not by placement geometry. See ShapedStacking-Plan.md (C1).

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
                    !ReferenceEquals(existing, ignoredA) &&
                    !ReferenceEquals(existing, ignoredB))
                    return false;
            }

            return true;
        }

        public bool TryPlace(PlacementRequest request, out Placement placement)
        {
            placement = null;
            if (!CanPlace(request))
                return false;

            var orientation = Topology.NormalizeOrientation(request.Orientation);
            var coveredOffsets = Topology.GetPlacementOffsets(request.Shape, orientation);
            var coveredIndices = GetCoveredIndices(
                request.AnchorIndex,
                request.Shape,
                orientation,
                PlacementBoundsMode.RequireAllInBounds);
            if (coveredIndices == null || coveredIndices.Count == 0)
                return false;

            placement = new Placement(
                Topology.ToCell(request.AnchorIndex),
                request.AnchorIndex,
                orientation,
                request.Shape,
                request.Stack,
                coveredIndices,
                coveredOffsets);

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
            int orientation,
            PlacementBoundsMode boundsMode)
        {
            if (!Topology.IsValidIndex(anchorIndex))
                return EmptyIndices;

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
            int orientation,
            PlacementBoundsMode boundsMode)
        {
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

                var coveredOffsets = Topology.GetPlacementOffsets(
                    placement.Shape,
                    placement.Orientation);
                shiftedPlacements.Add(new Placement(
                    Topology.ToCell(anchorIndex),
                    anchorIndex,
                    placement.Orientation,
                    placement.Shape,
                    placement.MutableStack,
                    covered,
                    coveredOffsets));
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
    }
}