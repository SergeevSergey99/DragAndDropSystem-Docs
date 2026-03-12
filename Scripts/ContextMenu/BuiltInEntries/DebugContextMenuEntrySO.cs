using UnityEngine;

namespace DragAndDropSystem.ContextMenu.BuiltInEntries
{
    [CreateAssetMenu(fileName = "DebugContextMenuEntry", menuName = "DragAndDrop/ContextMenu/Built-in/Debug Entry", order = 101)]
    public class DebugContextMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        public override bool CanShow(ContextMenuContext ctx)
        {
            if (ctx.Inventory == null || ctx.Slot == null || ctx.Item == null)
                return false;
            return true;
        }

        public override void Execute(ContextMenuContext ctx)
        {
            Debug.Log($"Item name: {ctx.Slot.Stack.Item.DisplayName}, stack size: {ctx.Slot.Stack.Count}");
        }
    }
}