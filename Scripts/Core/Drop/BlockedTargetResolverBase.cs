using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public abstract class BlockedTargetResolverBase
    {
        public virtual ISwapStrategy SwapStrategy => null;
        public virtual IAlternativePlacementStrategy AlternativePlacementStrategy => null;
    }
}
