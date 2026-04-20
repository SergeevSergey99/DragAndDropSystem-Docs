using System;
using System.Collections.Generic;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Core
{
    public interface IAlternativePlacementStrategy
    {
        IEnumerable<BaseSlot> EnumerateAlternativeSlots(
            List<BaseSlot> slots,
            IItemAdapter itemAdapter,
            BaseSlot excludeBaseSlot,
            Func<BaseSlot, IItemAdapter, bool> canUseAlternativeSlot);
    }
}
