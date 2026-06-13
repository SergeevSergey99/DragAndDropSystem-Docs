using System;
using UnityEngine;
using UDND.Inventories;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private BlockedTargetResolverBase _blockedTargetResolver = new FindAlternativeBlockedTargetResolver();
        [SerializeField, Tooltip("Allow partial transfer if only part of the requested amount fits.")]
        private bool _allowPartial = true;
        [SerializeField, Tooltip("How to process batch transfers: Atomic cancels the whole operation on the first error, BestEffort transfers whatever succeeds.")]
        private BatchMode _batchMode = BatchMode.BestEffort;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            var blockedTargetResolver = requested.HasValue && requested.Value.BlockedTargetResolver != null
                ? requested.Value.BlockedTargetResolver
                : _blockedTargetResolver ?? new FindAlternativeBlockedTargetResolver();

            var allowPartial = requested.HasValue && requested.Value.AllowPartial.HasValue
                ? requested.Value.AllowPartial.Value
                : _allowPartial;

            return new ResolvedDropPolicy(
                requested?.BlockedTargetResolution ??
                DropRequestPolicy.ResolveKind(blockedTargetResolver) ??
                BlockedTargetResolutionKind.AlternativeSlots,
                requested?.AlternativeOrderer ??
                DropRequestPolicy.ResolveOrderer(blockedTargetResolver) ??
                MergeFirstPlacementCandidateOrderer.Instance,
                requested?.AllowSameInventoryAlternativePlacement ??
                DropRequestPolicy.ResolveSameInventoryAlternative(blockedTargetResolver) ??
                true,
                requested?.PartialTransferMode ??
                (allowPartial
                    ? PartialTransferMode.Allow
                    : PartialTransferMode.RequireFull),
                _batchMode,
                blockedTargetResolver);
        }
    }
}
