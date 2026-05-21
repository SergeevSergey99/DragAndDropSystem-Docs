using System.Collections.Generic;
using UDND.Slots;

namespace UDND.Core
{
    public interface ISwapStrategy
    {
        IEnumerable<BaseSlot> EnumerateSwapTargets(SwapSearchContext context);
    }
}
