using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private BlockedTargetResolverBase _blockedTargetResolver = new FindAlternativeBlockedTargetResolver();
        [SerializeField, Tooltip("Used by strategies that support merge-on-drop behavior. Each strategy decides how this flag is interpreted.")]
        private bool _allowMergeOnDrop = true;
        [SerializeField, Tooltip("Allow partial transfer if only part of the requested amount fits.")]
        private bool _allowPartial = true;
        [SerializeField, Tooltip("How to process batch transfers: Atomic cancels the whole operation on the first error, BestEffort transfers whatever succeeds.")]
        private BatchMode _batchMode = BatchMode.BestEffort;

        public bool AllowMergeOnDrop => _allowMergeOnDrop;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            var blockedTargetResolver = requested.HasValue && requested.Value.BlockedTargetResolver != null
                ? requested.Value.BlockedTargetResolver
                : _blockedTargetResolver ?? new FindAlternativeBlockedTargetResolver();

            var allowPartial = requested.HasValue && requested.Value.AllowPartial.HasValue
                ? requested.Value.AllowPartial.Value
                : _allowPartial;

            return new ResolvedDropPolicy(
                blockedTargetResolver,
                allowPartial,
                _batchMode);
        }
    }
}
