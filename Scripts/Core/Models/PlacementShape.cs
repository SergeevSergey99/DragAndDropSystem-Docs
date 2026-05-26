using System;
using System.Collections.Generic;
using UnityEngine;

namespace UDND.Core
{
    public interface IPlacementShape
    {
        IReadOnlyList<Vector2Int> GetOffsets(PlacementOrientation orientation);
        bool SupportsOrientation(PlacementOrientation orientation);
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

        public IReadOnlyList<Vector2Int> GetOffsets(PlacementOrientation orientation)
            => _offsetsByOrientation[NormalizeOrientationIndex(orientation)];

        public bool SupportsOrientation(PlacementOrientation orientation) => true;

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

        private static int NormalizeOrientationIndex(PlacementOrientation orientation)
        {
            switch (orientation)
            {
                case PlacementOrientation.Rot90:
                    return 1;
                case PlacementOrientation.Rot180:
                    return 2;
                case PlacementOrientation.Rot270:
                    return 3;
                case PlacementOrientation.Rot0:
                default:
                    return 0;
            }
        }
    }

    public sealed class OffsetPlacementShape : IPlacementShape
    {
        private readonly IReadOnlyList<Vector2Int> _offsets;
        private readonly PlacementOrientation _supportedOrientation;

        public OffsetPlacementShape(
            IReadOnlyList<Vector2Int> offsets,
            PlacementOrientation supportedOrientation = PlacementOrientation.Rot0)
        {
            _offsets = CopyOffsets(offsets);
            _supportedOrientation = supportedOrientation;
        }

        public IReadOnlyList<Vector2Int> GetOffsets(PlacementOrientation orientation)
            => SupportsOrientation(orientation) ? _offsets : Array.Empty<Vector2Int>();

        public bool SupportsOrientation(PlacementOrientation orientation)
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

        public static bool IsSingleCell(IPlacementShape shape, PlacementOrientation orientation)
        {
            if (!TryGetOffsets(shape, orientation, out var offsets))
                return false;

            return offsets.Count == 1 && offsets[0] == Vector2Int.zero;
        }

        public static Vector2Int GetBoundingSize(IPlacementShape shape, PlacementOrientation orientation)
        {
            if (!TryGetOffsets(shape, orientation, out var offsets))
                return Vector2Int.zero;

            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < offsets.Count; i++)
            {
                maxX = Math.Max(maxX, offsets[i].x);
                maxY = Math.Max(maxY, offsets[i].y);
            }

            return new Vector2Int(maxX + 1, maxY + 1);
        }

        public static bool OffsetsEqual(
            IPlacementShape left,
            IPlacementShape right,
            PlacementOrientation orientation)
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
            PlacementOrientation orientation,
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
