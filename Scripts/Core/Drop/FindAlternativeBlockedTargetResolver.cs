using System;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class FindAlternativeBlockedTargetResolver : BlockedTargetResolverBase
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private IAlternativePlacementStrategy _alternativePlacementStrategy = new MergeFirstAlternativePlacementStrategy();

        public override BlockedTargetBehavior Behavior => BlockedTargetBehavior.FindAlternative;

        public void SetAlternativePlacementStrategy(IAlternativePlacementStrategy strategy)
        {
            _alternativePlacementStrategy = strategy;
        }

        public override AlternativePlacementMode GetAlternativePlacement()
        {
            return _alternativePlacementStrategy?.Mode ?? AlternativePlacementMode.MergeFirst;
        }
    }
}
