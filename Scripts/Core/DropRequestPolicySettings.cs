using System;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class DropRequestPolicySettings
    {
        [SerializeField] private bool _overrideBlockedTarget;
        [SerializeField, ShowIf(nameof(_overrideBlockedTarget))]
        private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;

        [SerializeField] private bool _overrideAlternativePlacement;
        [SerializeField, ShowIf(nameof(_overrideAlternativePlacement))]
        private AlternativePlacementMode _alternativePlacement = AlternativePlacementMode.MergeFirst;

        [SerializeField] private bool _overrideAllowPartial;
        [SerializeField, ShowIf(nameof(_overrideAllowPartial))]
        private bool _allowPartial = true;

        public DropRequestPolicy? TryBuild()
        {
            if (!_overrideBlockedTarget && !_overrideAlternativePlacement && !_overrideAllowPartial)
                return null;

            return new DropRequestPolicy(
                _overrideBlockedTarget ? _blockedTarget : (BlockedTargetBehavior?)null,
                _overrideAlternativePlacement ? _alternativePlacement : (AlternativePlacementMode?)null,
                _overrideAllowPartial ? _allowPartial : (bool?)null);
        }
    }
}
