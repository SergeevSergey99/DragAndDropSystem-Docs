using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class SwapBlockedTargetResolver : BlockedTargetResolverBase
    {
        public override bool SupportsSwap => true;
    }
}
