using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public abstract class BlockedTargetResolverBase
    {
        public abstract BlockedTargetBehavior Behavior { get; }

        public virtual AlternativePlacementMode GetAlternativePlacement()
        {
            return AlternativePlacementMode.MergeFirst;
        }
    }
}
