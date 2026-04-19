using System;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Core
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
        [SerializeField, HideInInspector]
        private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;
        [SerializeField, HideInInspector]
        private AlternativePlacementMode _alternativePlacement = AlternativePlacementMode.MergeFirst;

        public bool AllowMergeOnDrop => _allowMergeOnDrop;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            EnsureResolverInitialized();

            var configuredBlocked = _blockedTargetResolver?.Behavior ?? BlockedTargetBehavior.FindAlternative;
            var configuredAlternativePlacement = _blockedTargetResolver?.GetAlternativePlacement() ?? AlternativePlacementMode.MergeFirst;
            var blocked = requested.HasValue && requested.Value.BlockedTarget.HasValue
                ? requested.Value.BlockedTarget.Value
                : configuredBlocked;

            var alternativePlacement = requested.HasValue && requested.Value.AlternativePlacement.HasValue
                ? requested.Value.AlternativePlacement.Value
                : configuredAlternativePlacement;

            var allowPartial = requested.HasValue && requested.Value.AllowPartial.HasValue
                ? requested.Value.AllowPartial.Value
                : _allowPartial;

            return new ResolvedDropPolicy(
                blocked,
                allowPartial,
                _batchMode,
                alternativePlacement);
        }

        private void EnsureResolverInitialized()
        {
            if (_blockedTargetResolver != null)
                return;

            switch (_blockedTarget)
            {
                case BlockedTargetBehavior.Reject:
                    _blockedTargetResolver = new RejectBlockedTargetResolver();
                    break;

                case BlockedTargetBehavior.Swap:
                    _blockedTargetResolver = new SwapBlockedTargetResolver();
                    break;

                case BlockedTargetBehavior.FindAlternative:
                default:
                    _blockedTargetResolver = CreateFindAlternativeResolver(_alternativePlacement);
                    break;
            }
        }

        private static BlockedTargetResolverBase CreateFindAlternativeResolver(AlternativePlacementMode mode)
        {
            var resolver = new FindAlternativeBlockedTargetResolver();
            switch (mode)
            {
                case AlternativePlacementMode.EmptyFirst:
                    resolver.SetAlternativePlacementStrategy(new EmptyFirstAlternativePlacementStrategy());
                    break;
                case AlternativePlacementMode.MergeOnly:
                    resolver.SetAlternativePlacementStrategy(new MergeOnlyAlternativePlacementStrategy());
                    break;
                case AlternativePlacementMode.EmptyOnly:
                    resolver.SetAlternativePlacementStrategy(new EmptyOnlyAlternativePlacementStrategy());
                    break;
                case AlternativePlacementMode.MergeFirst:
                default:
                    resolver.SetAlternativePlacementStrategy(new MergeFirstAlternativePlacementStrategy());
                    break;
            }

            return resolver;
        }
    }
}
