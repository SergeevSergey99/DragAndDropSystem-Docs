using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional UI-facing inventory contract for pointer state and drop preview feedback.
    /// Keeps slot adapters and drag visuals independent from concrete inventory components.
    /// </summary>
    public interface IInventoryInteraction
    {
        void NotifyPointerEnter(BaseSlot baseSlot);
        void NotifyPointerExit(BaseSlot baseSlot);
        void NotifySlotInteracted(BaseSlot baseSlot);
        BaseSlot ResolveAutoTransferSlot();

        bool TryGetDropPreviewSlots(
            BaseSlot targetBaseSlot,
            DragContext context,
            out IReadOnlyList<BaseSlot> previewSlots,
            out bool canPlace);

        bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context);
        void ClearDropPreview();
    }
}
