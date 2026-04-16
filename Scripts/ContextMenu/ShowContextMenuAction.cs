using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Interaction;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.ContextMenu
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

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!ContextMenuManager.IsInstanceExist)
                return false;

            if (ContextMenuManager.AutoCreateInstance.IsOpen)
                return true;

            return inventory != null
                   && (ContextMenuManager.AutoCreateInstance.DefaultPreset != null
                       || inventory.GetComponent<ContextMenuBinder>() != null);
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (inventory == null)
            {
                ContextMenuManager.AutoCreateInstance.Hide();
                return ActionResult.Failed("Inventory is null");
            }
            
            var slot = adapter?.BaseSlot ?? inventory.ResolveAutoTransferSlot();

            var ctx = new ContextMenuContext
            {
                Inventory      = inventory,
                BaseSlot           = slot,
                ItemStack      = slot?.Stack,
                ScreenPosition = eventData?.position ?? Vector2.zero,
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