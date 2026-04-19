using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class SwapBlockedTargetResolver : BlockedTargetResolverBase
    {
        public override BlockedTargetBehavior Behavior => BlockedTargetBehavior.Swap;
    }
}
