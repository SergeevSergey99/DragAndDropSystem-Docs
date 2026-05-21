using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropRequestPolicySettings
    {
        [SerializeField] private bool _overrideBlockedTargetResolver;
        [SerializeReference, ShowIf(nameof(_overrideBlockedTargetResolver)), ManagedReferencePicker, InlineProperty, HideLabel]
        private BlockedTargetResolverBase _blockedTargetResolver = new FindAlternativeBlockedTargetResolver();

        [SerializeField] private bool _overrideAllowPartial;
        [SerializeField, ShowIf(nameof(_overrideAllowPartial))]
        private bool _allowPartial = true;

        public DropRequestPolicy? TryBuild()
        {
            if (!_overrideBlockedTargetResolver && !_overrideAllowPartial)
                return null;

            return new DropRequestPolicy(
                _overrideBlockedTargetResolver ? _blockedTargetResolver : null,
                _overrideAllowPartial ? _allowPartial : (bool?)null);
        }
    }
}
