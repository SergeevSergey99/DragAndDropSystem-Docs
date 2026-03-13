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
    /// Открывает контекстное меню для слота.
    /// Пункты меню берутся из <see cref="ContextMenuBinder"/> на GO инвентаря.
    /// Требует <see cref="ContextMenuManager"/> на сцене.
    /// </summary>
    [Serializable]
    public sealed class ShowContextMenuAction : AssetSafeSlotInteractionAction
    {
        public override string DisplayName => "Show Context Menu";

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!ContextMenuManager.IsInstanceExist)
                return false;

            if (ContextMenuManager.Instance.IsOpen)
                return true;

            return ContextMenuManager.Instance.DefaultPreset != null
                   || inventory != null && inventory.GetComponent<ContextMenuBinder>() != null;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (inventory == null)
            {
                ContextMenuManager.Instance.Hide();
                return ActionResult.Failed("Inventory is null");
            }

            var binder = inventory.GetComponent<ContextMenuBinder>();
            
            var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
            
            List<IContextMenuEntry> entries = new();
            if (binder == null)
            {
                if (ContextMenuManager.IsInstanceExist && ContextMenuManager.Instance.DefaultPreset != null)
                {
                    entries.AddRange(ContextMenuManager.Instance.DefaultPreset.Entries);
                }
                else
                {
                    ContextMenuManager.Instance.Hide();
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
                ContextMenuManager.Instance.Hide();
                return ActionResult.Failed("No context menu entries configured");
            }

            var ctx = new ContextMenuContext
            {
                Inventory      = inventory,
                Slot           = slot,
                Item           = slot?.Stack?.Item,
                ItemCount      = slot?.Stack?.Count ?? 0,
                ScreenPosition = eventData?.position ?? Vector2.zero,
                InputSource    = InputEventRouter.Instance.ResolveActiveFocusSource(inventory),
            };

            ContextMenuManager.Instance.Show(entries, ctx);
            return ActionResult.Succeeded();
        }
    }
}
