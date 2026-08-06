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

        /// <summary>
        /// Shows the preview using a probe the caller already ran for this hover, so the highlight
        /// and any per-slot feedback agree with the drop that would follow — same probe, same
        /// policy. Pass null only when there is no drop processor to ask.
        /// </summary>
        bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context, TransferProbe probe);

        /// <summary>
        /// Verdict of the preview currently on screen for this slot, if it is part of one.
        /// Lets feedback visuals read the decision the preview already made instead of probing
        /// again with a policy of their own.
        /// </summary>
        bool TryGetActiveDropVerdict(BaseSlot baseSlot, out DropVerdict verdict);

        void ClearDropPreview();
    }
}
