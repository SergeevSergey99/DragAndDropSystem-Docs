using System.Collections.Generic;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Core
{
    public interface ISwapStrategy
    {
        IEnumerable<BaseSlot> EnumerateSwapTargets(SwapSearchContext context);
    }
}
