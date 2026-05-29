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

            var inventory = snapshot?.Inventory;
            if (inventory == null)
                return false;

            return inventory != null
                   && (ContextMenuManager.AutoCreateInstance.DefaultPreset != null
                       || TryGetContextMenuBinder(inventory, out _));
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            var inventory = snapshot?.Inventory;
            if (inventory == null)
            {
                ContextMenuManager.AutoCreateInstance.Hide();
                return ActionResult.Failed("Inventory is null");
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

        private static bool TryGetContextMenuBinder(IInventory inventory, out ContextMenuBinder binder)
        {
            binder = null;
            if (inventory is not Component component)
                return false;

            binder = component.GetComponent<ContextMenuBinder>();
            return binder != null;
        }

        private static BaseSlot ResolveAutoTransferSlot(IInventory inventory)
        {
            return inventory is IInventoryInteraction interactionSurface
                ? interactionSurface.ResolveAutoTransferSlot()
                : null;
        }
    }
}
