using UDND.Inventories;
using UnityEngine;

namespace UDND.Slots
{
    public class CrossFeedbackSlot : UniversalSlot
    {
        [SerializeField] private GameObject _redCross;

        public override void Highlight(bool highlight)
        {
            base.Highlight(highlight);
            _redCross.SetActive(highlight && !AcceptsCurrentDrag());
        }

        private bool AcceptsCurrentDrag()
        {
            var manager = DragAndDropManager.AutoCreateInstance;
            if (!manager.IsDragging || Inventory is not IInventoryInteraction interaction)
                return true;

            return interaction.TryGetDropPreviewSlots(
                       this, manager.CurrentContext, out _, out bool canPlace)
                   && canPlace;
        }
    }
}