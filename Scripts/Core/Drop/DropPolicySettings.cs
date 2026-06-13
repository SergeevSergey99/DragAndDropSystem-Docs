using System;
using UnityEngine;
using UDND.Inventories;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeField]
        private BlockedTargetResolutionKind _blockedTargetResolution =
            BlockedTargetResolutionKind.AlternativeSlots;

        [SerializeReference, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.AlternativeSlots),
         ManagedReferencePicker, InlineProperty, HideLabel]
        private PlacementCandidateOrderer _alternativeOrderer =
            new MergeFirstPlacementCandidateOrderer();

        [SerializeField, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.AlternativeSlots),
         Tooltip("Allow a blocked same-inventory drop to use another placement.")]
        private bool _allowSameInventoryAlternativePlacement = true;

        [SerializeField, Tooltip("Allow partial transfer if only part of one entry fits.")]
        private bool _allowPartial = true;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            return new ResolvedDropPolicy(
                requested?.BlockedTargetResolution ?? _blockedTargetResolution,
                requested?.AlternativeOrderer ?? _alternativeOrderer ??
                MergeFirstPlacementCandidateOrderer.Instance,
                requested?.AllowSameInventoryAlternativePlacement ??
                _allowSameInventoryAlternativePlacement,
                requested?.PartialTransferMode ??
                (_allowPartial
                    ? PartialTransferMode.Allow
                    : PartialTransferMode.RequireFull));
        }
    }
}
