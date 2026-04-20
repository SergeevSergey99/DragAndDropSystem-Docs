using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public abstract class BlockedTargetResolverBase
    {
        public virtual bool SupportsSwap => false;
        public virtual IAlternativePlacementStrategy AlternativePlacementStrategy => null;
    }
}
