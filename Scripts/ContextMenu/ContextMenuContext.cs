using DragAndDropSystem.Core;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    public struct ContextMenuContext
    {
        /// <summary>Inventory on which the menu was opened.</summary>
        public Inventories.UniversalInventory Inventory;

        /// <summary>Slot that was clicked (can be null).</summary>
        public BaseSlot BaseSlot;

        /// <summary>Item in the slot. null if the slot is empty.</summary>
        public IItemAdapter ItemAdapter;

        /// <summary>Number of items in the stack.</summary>
        public int ItemCount;

        /// <summary>Screen position of the click.</summary>
        public Vector2 ScreenPosition;

        /// <summary>Input source (mouse / gamepad / etc.).</summary>
        public FocusSource InputSource;
    }
}
