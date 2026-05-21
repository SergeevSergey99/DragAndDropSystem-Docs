using UnityEngine;
using UDND.Core;
using UDND.Interaction;
using UDND.Slots;

namespace UDND.ContextMenu
{
    public struct ContextMenuContext
    {
        /// <summary>Inventory on which the menu was opened.</summary>
        public Inventories.UniversalInventory Inventory;

        /// <summary>Slot that was clicked (can be null).</summary>
        public BaseSlot BaseSlot;

        /// <summary>Item stack in the slot. null if the slot is empty.</summary>
        public IReadOnlyItemStack ItemStack;

        /// <summary>Screen position of the click.</summary>
        public Vector2 ScreenPosition;

        /// <summary>Input source (mouse / gamepad / etc.).</summary>
        public FocusSource InputSource;
    }
}
