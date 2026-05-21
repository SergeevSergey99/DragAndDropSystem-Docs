using System;
using System.Collections.Generic;
using UDND.Slots;

namespace UDND.Core
{
    [Serializable]
    public sealed class HintedTargetSwapStrategy : ISwapStrategy
    {
        public IEnumerable<BaseSlot> EnumerateSwapTargets(SwapSearchContext context)
        {
            if (context == null || !context.PreferHint)
                yield break;

            if (context.DragContext != null && context.DragContext.IsBatchDrag)
                yield break;

            if (context.HintedTargetBaseSlot == null)
                yield break;

            yield return context.HintedTargetBaseSlot;
        }
    }
}
