using System;
using System.Collections.Generic;
using UDND.Core;
using UnityEngine;

namespace UDND.Inventories
{
    internal static class PlacementSnapshotCodec
    {
        public static InventorySnapshot Capture(int slotCount, IReadOnlyCollection<Placement> placements)
        {
            var placementSnapshot = new List<InventoryPlacementState>(placements?.Count ?? 0);
            if (placements != null)
            {
                foreach (var placement in placements)
                {
                    if (placement == null || placement.MutableStack == null || placement.MutableStack.IsEmpty)
                        continue;

                    placementSnapshot.Add(new InventoryPlacementState(
                        placement.AnchorIndex,
                        placement.MutableStack.Adapters,
                        placement.Orientation,
                        placement.BoundingSize,
                        placement.CoveredIndices,
                        ResolvePlacementOffsets(placement)));
                }
            }

            return new InventorySnapshot(slotCount, placementSnapshot);
        }

        public static bool TryBuildPlacementRequests(
            InventorySnapshot snapshot,
            IInventoryTopology topology,
            out List<PlacementRequest> requests,
            out InventoryPlacementState failedPlacement)
        {
            requests = new List<PlacementRequest>(snapshot?.Placements?.Count ?? 0);
            failedPlacement = default(InventoryPlacementState);
            if (snapshot?.Placements == null)
                return true;

            var snapshotStore = new PlacementStore(topology);
            for (int i = 0; i < snapshot.Placements.Count; i++)
            {
                var placementState = snapshot.Placements[i];
                if (placementState.IsEmpty)
                    continue;

                if (!ItemStack.TryCreate(placementState.Adapters, out var restoredStack))
                {
                    failedPlacement = placementState;
                    return false;
                }

                var request = new PlacementRequest(
                    restoredStack,
                    placementState.AnchorIndex,
                    placementState.Orientation,
                    ResolveShape(placementState));

                if (!snapshotStore.TryPlace(request, out _))
                {
                    failedPlacement = placementState;
                    return false;
                }

                requests.Add(request);
            }

            return true;
        }

        private static IPlacementShape ResolveShape(InventoryPlacementState placementState)
        {
            return placementState.CoveredOffsets != null && placementState.CoveredOffsets.Count > 0
                ? new OffsetPlacementShape(placementState.CoveredOffsets, placementState.Orientation)
                : PlacementShapeUtility.FromRectSize(placementState.BoundingSize);
        }

        private static IReadOnlyList<Vector2Int> ResolvePlacementOffsets(Placement placement)
        {
            return placement?.CoveredOffsets ?? Array.Empty<Vector2Int>();
        }
    }
}
