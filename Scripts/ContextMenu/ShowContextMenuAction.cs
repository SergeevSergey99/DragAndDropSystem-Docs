using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Interaction;

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

            return snapshot?.Inventory != null
                   && (ContextMenuManager.AutoCreateInstance.DefaultPreset != null
                       || snapshot.Inventory.GetComponent<ContextMenuBinder>() != null);
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            var inventory = snapshot?.Inventory;
            if (inventory == null)
            {
                ContextMenuManager.AutoCreateInstance.Hide();
                return ActionResult.Failed("Inventory is null");
            }
            
            var slot = snapshot.ActiveSlot ?? inventory.ResolveAutoTransferSlot();

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
    }
}
