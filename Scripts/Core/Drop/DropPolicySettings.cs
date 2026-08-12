using System;
using UnityEngine;
using UnityEngine.Serialization;
using UDND.Inventories;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeField]
        private BlockedTargetResolutionKind _blockedTargetResolution =
            BlockedTargetResolutionKind.FindAlternative;

        [SerializeReference, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.FindAlternative),
         ManagedReferencePicker, InlineProperty, HideLabel]
        private PlacementCandidateOrderer _alternativeOrderer =
            new MergeFirstPlacementCandidateOrderer();

        [SerializeField, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.FindAlternative),
         Tooltip("Allow a blocked same-inventory drop to use another placement.")]
        private bool _allowSameInventoryAlternativePlacement = false;

        [SerializeField, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.Swap),
         FormerlySerializedAs("_multiSwapMode"),
         Tooltip("How many placements one incoming item may displace. " +
                 "Only affects inventories with multi-cell footprints: where an item always " +
                 "occupies exactly one cell, both modes behave identically.")]
        private SwapDisplacementMode _swapDisplacement = SwapDisplacementMode.SinglePlacement;

        [SerializeField, Tooltip("Allow partial transfer if only part of one entry fits.")]
        private bool _allowPartial = true;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            return new ResolvedDropPolicy(
                requested?.BlockedTargetResolution ?? _blockedTargetResolution,
                requested?.AlternativeOrderer ?? _alternativeOrderer,
                requested?.AllowSameInventoryAlternativePlacement ?? _allowSameInventoryAlternativePlacement,
                requested?.PartialTransferMode ?? (_allowPartial
                    ? PartialTransferMode.Allow
                    : PartialTransferMode.RequireFull),
                requested?.SwapDisplacementMode ?? _swapDisplacement);
        }
    }
}
