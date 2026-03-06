using System;
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
    public sealed class ShowContextMenuAction : SlotInteractionAction
    {
        public override string DisplayName => "Show Context Menu";

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => inventory != null && ContextMenuManager.IsInstanceExist;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (inventory == null)
                return ActionResult.Failed("Inventory is null");

            var binder = inventory.GetComponent<ContextMenuBinder>();
            if (binder == null)
                return ActionResult.Failed("No ContextMenuBinder on inventory");

            var slot    = adapter?.Slot;
            var isEmpty = slot == null || slot.IsEmpty;
            var entries = binder.GetEntries(isEmpty);

            if (entries.Count == 0)
                return ActionResult.Failed("No context menu entries configured");

            var ctx = new ContextMenuContext
            {
                Inventory      = inventory,
                Slot           = slot,
                Item           = slot?.Stack?.Item,
                ItemCount      = slot?.Stack?.Count ?? 0,
                ScreenPosition = eventData?.position ?? Vector2.zero,
                InputSource    = eventData != null ? FocusSource.Mouse : FocusSource.Gamepad,
            };

            ContextMenuManager.Instance.Show(entries, ctx);
            return ActionResult.Succeeded();
        }
    }
}
