using System;
using System.Collections.Generic;
using UDND.Slots;

namespace UDND.Core
{
    [Serializable]
    public sealed class EmptyOnlyAlternativePlacementStrategy : IAlternativePlacementStrategy
    {
        public IEnumerable<BaseSlot> EnumerateAlternativeSlots(
            List<BaseSlot> slots,
            IItemAdapter itemAdapter,
            BaseSlot excludeBaseSlot,
            Func<BaseSlot, IItemAdapter, bool> canUseAlternativeSlot)
        {
            if (slots == null || itemAdapter == null || canUseAlternativeSlot == null)
                yield break;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || ReferenceEquals(slot, excludeBaseSlot) || !slot.IsEmpty)
                    continue;
                if (!canUseAlternativeSlot(slot, itemAdapter))
                    continue;

                yield return slot;
            }
        }
    }
}
