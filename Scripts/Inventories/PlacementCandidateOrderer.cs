using System;
using System.Collections.Generic;

namespace UDND.Inventories
{
    [Serializable]
    public abstract class PlacementCandidateOrderer
    {
        public abstract IEnumerable<PlacementCandidate> Order(
            PlacementCandidateSource source,
            InventoryAcceptanceRequest request);

        protected static IEnumerable<PlacementCandidate> ByKind(
            PlacementCandidateSource source,
            PlacementCandidateKind first,
            PlacementCandidateKind second,
            bool includeThird)
        {
            if (source == null)
                yield break;

            foreach (var candidate in source)
                if (candidate.Kind == first)
                    yield return candidate;

            foreach (var candidate in source)
                if (candidate.Kind == second)
                    yield return candidate;

            if (!includeThird)
                yield break;

            foreach (var candidate in source)
                if (candidate.Kind == PlacementCandidateKind.NewDynamicSlot)
                    yield return candidate;
        }
    }

    [Serializable]
    public sealed class NaturalPlacementCandidateOrderer : PlacementCandidateOrderer
    {
        public static readonly NaturalPlacementCandidateOrderer Instance =
            new NaturalPlacementCandidateOrderer();

        public override IEnumerable<PlacementCandidate> Order(
            PlacementCandidateSource source,
            InventoryAcceptanceRequest request)
            => source != null
                ? source
                : Array.Empty<PlacementCandidate>();
    }

    [Serializable]
    public sealed class MergeFirstPlacementCandidateOrderer : PlacementCandidateOrderer
    {
        public override IEnumerable<PlacementCandidate> Order(
            PlacementCandidateSource source,
            InventoryAcceptanceRequest request)
            => ByKind(source, PlacementCandidateKind.Merge, PlacementCandidateKind.Create, true);
    }

    [Serializable]
    public sealed class EmptyFirstPlacementCandidateOrderer : PlacementCandidateOrderer
    {
        public override IEnumerable<PlacementCandidate> Order(
            PlacementCandidateSource source,
            InventoryAcceptanceRequest request)
            => ByKind(source, PlacementCandidateKind.Create, PlacementCandidateKind.Merge, true);
    }

    [Serializable]
    public sealed class MergeOnlyPlacementCandidateOrderer : PlacementCandidateOrderer
    {
        public override IEnumerable<PlacementCandidate> Order(
            PlacementCandidateSource source,
            InventoryAcceptanceRequest request)
        {
            if (source == null)
                yield break;

            foreach (var candidate in source)
                if (candidate.Kind == PlacementCandidateKind.Merge)
                    yield return candidate;
        }
    }

    [Serializable]
    public sealed class EmptyOnlyPlacementCandidateOrderer : PlacementCandidateOrderer
    {
        public override IEnumerable<PlacementCandidate> Order(
            PlacementCandidateSource source,
            InventoryAcceptanceRequest request)
        {
            if (source == null)
                yield break;

            foreach (var candidate in source)
                if (candidate.Kind == PlacementCandidateKind.Create ||
                    candidate.Kind == PlacementCandidateKind.NewDynamicSlot)
                    yield return candidate;
        }
    }
}
