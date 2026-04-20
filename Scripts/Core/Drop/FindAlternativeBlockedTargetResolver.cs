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

        public override IAlternativePlacementStrategy AlternativePlacementStrategy =>
            _alternativePlacementStrategy ?? (_alternativePlacementStrategy = new MergeFirstAlternativePlacementStrategy());

        public void SetAlternativePlacementStrategy(IAlternativePlacementStrategy strategy)
        {
            _alternativePlacementStrategy = strategy ?? new MergeFirstAlternativePlacementStrategy();
        }
    }
}
