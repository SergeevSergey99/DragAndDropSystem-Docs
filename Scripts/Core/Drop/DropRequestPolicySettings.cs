using System;
using UnityEngine;
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

        [SerializeField] private bool _overrideAllowPartial;
        [SerializeField, ShowIf(nameof(_overrideAllowPartial))]
        private bool _allowPartial = true;

        private bool ShowAlternativeOrderer =>
            _overrideBlockedTargetResolution &&
            _blockedTargetResolution == BlockedTargetResolutionKind.FindAlternative;

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
                    : (PartialTransferMode?)null);
        }
    }
}
