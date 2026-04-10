using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.ContextMenu
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

            var binder = inventory.GetComponent<ContextMenuBinder>();
            
            var slot = adapter?.BaseSlot ?? inventory.ResolveAutoTransferSlot();
            
            List<IContextMenuEntry> entries = new();
            if (binder == null)
            {
                if (ContextMenuManager.IsInstanceExist && ContextMenuManager.AutoCreateInstance.DefaultPreset != null)
                {
                    entries.AddRange(ContextMenuManager.AutoCreateInstance.DefaultPreset.Entries);
                }
                else
                {
                    ContextMenuManager.AutoCreateInstance.Hide();
                    return ActionResult.Failed("No ContextMenuBinder on inventory");
                }
            }
            else
            {
                var isEmpty = slot == null || slot.IsEmpty;
                entries = binder.GetEntries(isEmpty);
            }

            if (entries.Count == 0)
            {
                ContextMenuManager.AutoCreateInstance.Hide();
                return ActionResult.Failed("No context menu entries configured");
            }

            var ctx = new ContextMenuContext
            {
                Inventory      = inventory,
                BaseSlot           = slot,
                ItemAdapter           = slot?.Stack?.PrimaryAdapter,
                ItemCount      = slot?.Stack?.Count ?? 0,
                ScreenPosition = eventData?.position ?? Vector2.zero,
                InputSource    = InputEventRouter.AutoCreateInstance.ResolveActiveFocusSource(inventory),
            };

            ContextMenuManager.AutoCreateInstance.Show(entries, ctx);
            return ActionResult.Succeeded();
        }
    }
}
