using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Slots
{
    /// <summary>
    /// Arguments for a slot hover event.
    /// Contains all information about the slot, item, and position for use in tooltips and other systems.
    /// </summary>
    public class SlotHoverEventArgs
    {
        /// <summary>
        /// Item in the slot (can be null if the slot is empty)
        /// </summary>
        public IItemAdapter ItemAdapter { get; }

        /// <summary>
        /// Slot being hovered
        /// </summary>
        public BaseSlot BaseSlot { get; }

        /// <summary>
        /// Cursor position in screen coordinates
        /// </summary>
        public Vector2 ScreenPosition { get; set; }

        /// <summary>
        /// Slot RectTransform (for positioning a tooltip relative to the slot)
        /// </summary>
        public RectTransform SlotRectTransform { get; }

        /// <summary>
        /// True if this is an enter event (OnPointerEnter), false if it is an exit event (OnPointerExit)
        /// </summary>
        public bool IsEnter { get; }

        /// <summary>
        /// Inventory that owns the slot
        /// </summary>
        public IInventory Inventory { get; }

        /// <summary>
        /// Slot index within the inventory
        /// </summary>
        public int SlotIndex { get; }

        /// <summary>
        /// Flag used to cancel further event processing (can be used by derived classes)
        /// </summary>
        public bool Cancel { get; set; }

        public SlotHoverEventArgs(
            IItemAdapter itemAdapter,
            BaseSlot baseSlot,
            Vector2 screenPosition,
            RectTransform rectTransform,
            bool isEnter)
        {
            ItemAdapter = itemAdapter;
            BaseSlot = baseSlot;
            ScreenPosition = screenPosition;
            SlotRectTransform = rectTransform;
            IsEnter = isEnter;
            Inventory = baseSlot?.Inventory;
            SlotIndex = baseSlot?.Index ?? -1;
            Cancel = false;
        }

        /// <summary>
        /// Check whether the slot contains an item
        /// </summary>
        public bool HasItem => ItemAdapter != null;

        /// <summary>
        /// Check whether the slot is empty
        /// </summary>
        public bool IsEmpty => ItemAdapter == null;

        /// <summary>
        /// Get the slot position in world coordinates
        /// </summary>
        public Vector3 GetSlotWorldPosition()
        {
            return SlotRectTransform != null ? SlotRectTransform.position : Vector3.zero;
        }

        /// <summary>
        /// Get the slot size
        /// </summary>
        public Vector2 GetSlotSize()
        {
            return SlotRectTransform != null ? SlotRectTransform.rect.size : Vector2.zero;
        }

        public override string ToString()
        {
            return $"SlotHover[{(IsEnter ? "Enter" : "Exit")}] _PrimaryAdapter: {ItemAdapter?.DisplayName ?? "Empty"}, Slot: {SlotIndex}, Pos: {ScreenPosition}";
        }
    }
}