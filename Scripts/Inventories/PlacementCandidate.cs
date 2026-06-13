using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public enum PlacementCandidateKind : byte
    {
        Merge,
        Create,
        NewDynamicSlot
    }

    public readonly struct PlacementCandidate
    {
        private PlacementCandidate(
            PlacementCandidateKind kind,
            Placement targetPlacement,
            BaseSlot anchor,
            PlacementOrientation orientation,
            IPlacementShape shape,
            int capacity)
        {
            Kind = kind;
            TargetPlacement = targetPlacement;
            Anchor = anchor;
            Orientation = orientation;
            Shape = shape;
            Capacity = capacity;
        }

        public PlacementCandidateKind Kind { get; }
        public Placement TargetPlacement { get; }
        public BaseSlot Anchor { get; }
        public PlacementOrientation Orientation { get; }
        public IPlacementShape Shape { get; }
        public int Capacity { get; }

        // Transitional aliases for acceptance callers.
        public ISlot Slot => Anchor;
        public int RemainingCapacity => Capacity;

        public static PlacementCandidate Merge(Placement placement, BaseSlot anchor, int capacity)
            => new PlacementCandidate(
                PlacementCandidateKind.Merge,
                placement,
                anchor,
                placement?.Orientation ?? PlacementOrientation.Rot0,
                placement?.Shape,
                capacity);

        public static PlacementCandidate Merge(
            BaseSlot anchor,
            PlacementOrientation orientation,
            IPlacementShape shape,
            int capacity)
            => new PlacementCandidate(
                PlacementCandidateKind.Merge,
                null,
                anchor,
                orientation,
                shape,
                capacity);

        public static PlacementCandidate Create(
            BaseSlot anchor,
            PlacementOrientation orientation,
            IPlacementShape shape,
            int capacity)
            => new PlacementCandidate(
                PlacementCandidateKind.Create,
                null,
                anchor,
                orientation,
                shape,
                capacity);

        public static PlacementCandidate NewDynamicSlot(
            PlacementOrientation orientation,
            IPlacementShape shape,
            int capacity)
            => new PlacementCandidate(
                PlacementCandidateKind.NewDynamicSlot,
                null,
                null,
                orientation,
                shape,
                capacity);
    }
}
