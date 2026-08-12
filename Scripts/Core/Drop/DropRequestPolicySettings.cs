using System;
using UnityEngine;
using UnityEngine.Serialization;
using UDND.Inventories;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropRequestPolicySettings
    {
        [SerializeField] private bool _overrideBlockedTargetResolution;
        [SerializeField, ShowIf(nameof(_overrideBlockedTargetResolution))]
        private BlockedTargetResolutionKind _blockedTargetResolution =
            BlockedTargetResolutionKind.FindAlternative;

        [SerializeReference, ShowIf(nameof(ShowAlternativeOrderer)),
         ManagedReferencePicker, InlineProperty, HideLabel]
        private PlacementCandidateOrderer _alternativeOrderer =
            new MergeFirstPlacementCandidateOrderer();

        [SerializeField, ShowIf(nameof(ShowAlternativeOrderer))]
        private bool _allowSameInventoryAlternativePlacement = true;

        [SerializeField, ShowIf(nameof(ShowSwapDisplacement)),
         FormerlySerializedAs("_multiSwapMode"),
         Tooltip("How many placements one incoming item may displace. " +
                 "Only affects inventories with multi-cell footprints: where an item always " +
                 "occupies exactly one cell, both modes behave identically.")]
        private SwapDisplacementMode _swapDisplacement = SwapDisplacementMode.SinglePlacement;

        [SerializeField, ShowIf(nameof(ShowSwapDisplacement)),
         Tooltip("What to do when a displaced item does not fit the position mirroring its offset. " +
                 "VacatedArea lets it take the nearest free spot inside the area the two items " +
                 "exchange, instead of refusing the swap.")]
        private SwapDisplacementFallback _swapDisplacementFallback = SwapDisplacementFallback.MirroredOnly;

        [SerializeField] private bool _overrideAllowPartial;
        [SerializeField, ShowIf(nameof(_overrideAllowPartial))]
        private bool _allowPartial = true;

        private bool ShowAlternativeOrderer =>
            _overrideBlockedTargetResolution &&
            _blockedTargetResolution == BlockedTargetResolutionKind.FindAlternative;

        private bool ShowSwapDisplacement =>
            _overrideBlockedTargetResolution &&
            _blockedTargetResolution == BlockedTargetResolutionKind.Swap;

        public DropRequestPolicy? TryBuild()
        {
            if (!_overrideBlockedTargetResolution && !_overrideAllowPartial)
                return null;

            return new DropRequestPolicy(
                _overrideBlockedTargetResolution
                    ? _blockedTargetResolution
                    : (BlockedTargetResolutionKind?)null,
                ShowAlternativeOrderer ? _alternativeOrderer : null,
                ShowAlternativeOrderer
                    ? _allowSameInventoryAlternativePlacement
                    : (bool?)null,
                _overrideAllowPartial
                    ? _allowPartial
                        ? PartialTransferMode.Allow
                        : PartialTransferMode.RequireFull
                    : (PartialTransferMode?)null,
                ShowSwapDisplacement
                    ? _swapDisplacement
                    : (SwapDisplacementMode?)null,
                ShowSwapDisplacement
                    ? _swapDisplacementFallback
                    : (SwapDisplacementFallback?)null);
        }
    }
}
