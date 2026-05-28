using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Interaction;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.ContextMenu
{
    /// <summary>
    /// Opens the context menu for a slot.
    /// Menu entries are taken from <see cref="ContextMenuBinder"/> on the inventory GameObject.
    /// Requires <see cref="ContextMenuManager"/> in the scene.
    /// </summary>
    [Serializable]
    public sealed class ShowContextMenuAction : AssetSafeSlotInteractionAction
    {
        public override string DisplayName => "Show Context Menu";

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
        {
            if (!ContextMenuManager.IsInstanceExist)
                return false;

            if (ContextMenuManager.AutoCreateInstance.IsOpen)
                return true;

            if (snapshot?.Inventory is not UniversalInventory inventory)
                return false;

            return inventory != null
                   && (ContextMenuManager.AutoCreateInstance.DefaultPreset != null
                       || inventory.GetComponent<ContextMenuBinder>() != null);
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (snapshot?.Inventory is not UniversalInventory inventory)
            {
                ContextMenuManager.AutoCreateInstance.Hide();
                return ActionResult.Failed("Context menu requires UniversalInventory");
            }
            
            var slot = snapshot.ActiveSlot ?? ResolveAutoTransferSlot(inventory);

            var ctx = new ContextMenuContext
            {
                Inventory      = inventory,
                BaseSlot           = slot,
                ItemStack      = slot?.Stack,
                ScreenPosition = snapshot.PointerEventData?.position ?? Vector2.zero,
                InputSource    = InputEventRouter.AutoCreateInstance.ResolveActiveFocusSource(inventory),
            };
            
            List<IContextMenuEntry> entries = ContextMenuManager.AutoCreateInstance.GetEntries(ctx);

            if (entries.Count == 0)
            {
                ContextMenuManager.AutoCreateInstance.Hide();
                return ActionResult.Failed("No context menu entries configured");
            }

            ContextMenuManager.AutoCreateInstance.Show(entries, ctx);
            return ActionResult.Succeeded();
        }

        private static BaseSlot ResolveAutoTransferSlot(IInventory inventory)
        {
            return inventory is IInventoryInteractionSurface interactionSurface
                ? interactionSurface.ResolveAutoTransferSlot()
                : null;
        }
    }
}
