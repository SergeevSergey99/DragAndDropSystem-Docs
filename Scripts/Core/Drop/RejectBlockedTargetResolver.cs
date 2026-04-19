using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class RejectBlockedTargetResolver : BlockedTargetResolverBase
    {
        public override BlockedTargetBehavior Behavior => BlockedTargetBehavior.Reject;
    }
}
