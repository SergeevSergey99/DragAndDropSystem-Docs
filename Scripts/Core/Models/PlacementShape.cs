using System;
using System.Collections.Generic;
using UnityEngine;

namespace UDND.Core
{
    public interface IPlacementShape
    {
        IReadOnlyList<Vector2Int> GetOffsets(int orientation);
        bool SupportsOrientation(int orientation);
    }

    public interface IItemPlacementShapeProvider
    {
        IPlacementShape PlacementShape { get; }
    }

    public sealed class RectPlacementShape : IPlacementShape, IEquatable<RectPlacementShape>
    {
        private readonly IReadOnlyList<Vector2Int>[] _offsetsByOrientation;

        public RectPlacementShape(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            _offsetsByOrientation = new[]
            {
                BuildOffsets(Width, Height),
                BuildOffsets(Height, Width),
                BuildOffsets(Width, Height),
                BuildOffsets(Height, Width)
            };
        }

        public int Width { get; }
        public int Height { get; }

        public static RectPlacementShape One { get; } = new RectPlacementShape(1, 1);

        public IReadOnlyList<Vector2Int> GetOffsets(int orientation)
            => _offsetsByOrientation[NormalizeOrientationIndex(orientation)];

        public bool SupportsOrientation(int orientation) => true;

        public bool Equals(RectPlacementShape other)
            => other != null && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is RectPlacementShape other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Height;
        public override string ToString() => $"{Width}x{Height}";

        private static IReadOnlyList<Vector2Int> BuildOffsets(int width, int height)
        {
            var offsets = new Vector2Int[Math.Max(1, width) * Math.Max(1, height)];
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    offsets[index++] = new Vector2Int(x, y);
            }

            return Array.AsReadOnly(offsets);
        }

        private static int NormalizeOrientationIndex(int orientation)
            => OrientationStepUtility.Normalize(orientation, 4);
    }

    public sealed class OffsetPlacementShape : IPlacementShape
    {
        private readonly IReadOnlyList<Vector2Int> _offsets;
        private readonly int _supportedOrientation;

        public OffsetPlacementShape(
            IReadOnlyList<Vector2Int> offsets,
            int supportedOrientation = 0)
        {
            _offsets = CopyOffsets(offsets);
            _supportedOrientation = supportedOrientation;
        }

        public IReadOnlyList<Vector2Int> GetOffsets(int orientation)
            => SupportsOrientation(orientation) ? _offsets : Array.Empty<Vector2Int>();

        public bool SupportsOrientation(int orientation)
            => orientation == _supportedOrientation;

        private static IReadOnlyList<Vector2Int> CopyOffsets(IReadOnlyList<Vector2Int> offsets)
        {
            if (offsets == null || offsets.Count == 0)
                return Array.AsReadOnly(new[] { Vector2Int.zero });

            var copy = new Vector2Int[offsets.Count];
            for (int i = 0; i < offsets.Count; i++)
                copy[i] = offsets[i];
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// Arbitrary (non-rectangular) footprint authored as a set of cell offsets.
    /// Precomputes the four 90-degree rotations using the same per-cell transform as
    /// <see cref="RectGridTopology.RotateOffset"/>, so covered cells stay consistent with the
    /// topology's grab-offset rotation. Mirrors <see cref="RectPlacementShape"/>'s 4-step model.
    /// </summary>
    public sealed class ComplexPlacementShape : IPlacementShape
    {
        private const int OrientationSteps = 4;

        private readonly IReadOnlyList<Vector2Int>[] _offsetsByOrientation;

        public ComplexPlacementShape(IReadOnlyList<Vector2Int> offsets)
        {
            var normalized = Normalize(offsets);
            _offsetsByOrientation = new IReadOnlyList<Vector2Int>[OrientationSteps];

            var current = normalized;
            for (int step = 0; step < OrientationSteps; step++)
            {
                // Order cells row-major so covered indices stay ascending and deterministic,
                // matching RectPlacementShape and the rest of the placement code.
                Array.Sort(current, CompareCells);
                // Anchor the footprint at its first occupied cell (row-major), not the bounding-box
                // corner: the corner can be an empty notch, so anchoring there would let the anchor
                // index name a cell the item doesn't occupy. The rotation chain below keeps operating
                // on the bbox-normalized `current`; only the stored copy is rebased to first-occupied.
                _offsetsByOrientation[step] = RebaseToFirstOccupied(current);
                current = RotateClockwise(current);
            }
        }

        private static IReadOnlyList<Vector2Int> RebaseToFirstOccupied(Vector2Int[] sortedOffsets)
        {
            var origin = sortedOffsets[0];
            var result = new Vector2Int[sortedOffsets.Length];
            for (int i = 0; i < sortedOffsets.Length; i++)
                result[i] = sortedOffsets[i] - origin;
            return Array.AsReadOnly(result);
        }

        private static int CompareCells(Vector2Int a, Vector2Int b)
        {
            int byY = a.y.CompareTo(b.y);
            return byY != 0 ? byY : a.x.CompareTo(b.x);
        }

        public IReadOnlyList<Vector2Int> GetOffsets(int orientation)
            => _offsetsByOrientation[OrientationStepUtility.Normalize(orientation, OrientationSteps)];

        public bool SupportsOrientation(int orientation) => true;

        private static Vector2Int[] Normalize(IReadOnlyList<Vector2Int> offsets)
        {
            if (offsets == null || offsets.Count == 0)
                return new[] { Vector2Int.zero };

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int i = 0; i < offsets.Count; i++)
            {
                minX = Math.Min(minX, offsets[i].x);
                minY = Math.Min(minY, offsets[i].y);
            }

            var result = new Vector2Int[offsets.Count];
            for (int i = 0; i < offsets.Count; i++)
                result[i] = new Vector2Int(offsets[i].x - minX, offsets[i].y - minY);
            return result;
        }

        private static Vector2Int[] RotateClockwise(Vector2Int[] offsets)
        {
            var size = PlacementShapeUtility.GetBoundingSize(offsets);
            var result = new Vector2Int[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                result[i] = new Vector2Int(size.y - 1 - offsets[i].y, offsets[i].x);
            return result;
        }
    }

    public static class PlacementShapeUtility
    {
        public static IPlacementShape Resolve(IItemAdapter adapter)
        {
            if (adapter is IItemPlacementShapeProvider shapeProvider && shapeProvider.PlacementShape != null)
                return shapeProvider.PlacementShape;

            return RectPlacementShape.One;
        }

        public static IPlacementShape FromRectSize(Vector2Int size)
        {
            return new RectPlacementShape(size.x, size.y);
        }

        public static bool IsSingleCell(IPlacementShape shape, int orientation)
        {
            if (!TryGetOffsets(shape, orientation, out var offsets))
                return false;

            return offsets.Count == 1 && offsets[0] == Vector2Int.zero;
        }

        public static bool IsSingleCell(
            IPlacementShape shape,
            int orientation,
            IInventoryTopology topology)
        {
            if (topology == null)
                return IsSingleCell(shape, orientation);

            var offsets = topology.GetPlacementOffsets(shape, orientation);
            return offsets != null &&
                   offsets.Count == 1 &&
                   offsets[0] == Vector2Int.zero;
        }

        public static Vector2Int GetBoundingSize(IPlacementShape shape, int orientation)
        {
            if (!TryGetOffsets(shape, orientation, out var offsets))
                return Vector2Int.zero;

            return GetBoundingSize(offsets);
        }

        public static Vector2Int GetBoundingSize(
            IPlacementShape shape,
            int orientation,
            IInventoryTopology topology)
        {
            if (topology == null)
                return GetBoundingSize(shape, orientation);

            return GetBoundingSize(topology.GetPlacementOffsets(shape, orientation));
        }

        /// <summary>
        /// Position of the shape's anchor (its first occupied cell, at offset (0,0)) relative to the
        /// bounding-box corner. Equals -min(offsets). Zero for rectangles. Used to convert between
        /// anchor-relative and bbox-corner-relative coordinates when rotating offsets.
        /// </summary>
        public static Vector2Int GetAnchorOffsetInBounds(IPlacementShape shape, int orientation)
        {
            if (!TryGetOffsets(shape, orientation, out var offsets))
                return Vector2Int.zero;

            int minX = offsets[0].x;
            int minY = offsets[0].y;
            for (int i = 1; i < offsets.Count; i++)
            {
                minX = Math.Min(minX, offsets[i].x);
                minY = Math.Min(minY, offsets[i].y);
            }

            return new Vector2Int(-minX, -minY);
        }

        public static Vector2Int GetBoundingSize(IReadOnlyList<Vector2Int> offsets)
        {
            if (offsets == null || offsets.Count == 0)
                return Vector2Int.zero;

            int minX = offsets[0].x;
            int minY = offsets[0].y;
            int maxX = offsets[0].x;
            int maxY = offsets[0].y;
            for (int i = 0; i < offsets.Count; i++)
            {
                minX = Math.Min(minX, offsets[i].x);
                minY = Math.Min(minY, offsets[i].y);
                maxX = Math.Max(maxX, offsets[i].x);
                maxY = Math.Max(maxY, offsets[i].y);
            }

            return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        }

        public static bool OffsetsEqual(
            IPlacementShape left,
            IPlacementShape right,
            int orientation)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            if (!left.SupportsOrientation(orientation) || !right.SupportsOrientation(orientation))
                return false;

            var leftOffsets = left.GetOffsets(orientation);
            var rightOffsets = right.GetOffsets(orientation);
            if (leftOffsets == null || rightOffsets == null || leftOffsets.Count != rightOffsets.Count)
                return false;

            for (int i = 0; i < leftOffsets.Count; i++)
            {
                if (leftOffsets[i] != rightOffsets[i])
                    return false;
            }

            return true;
        }

        private static bool TryGetOffsets(
            IPlacementShape shape,
            int orientation,
            out IReadOnlyList<Vector2Int> offsets)
        {
            shape ??= RectPlacementShape.One;
            offsets = null;

            if (!shape.SupportsOrientation(orientation))
                return false;

            offsets = shape.GetOffsets(orientation);
            return offsets != null && offsets.Count > 0;
        }
    }
}
