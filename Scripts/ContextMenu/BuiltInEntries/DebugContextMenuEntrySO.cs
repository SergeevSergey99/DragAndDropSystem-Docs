using UnityEngine;

namespace DragAndDropSystem.ContextMenu.BuiltInEntries
{
    [CreateAssetMenu(fileName = "DebugContextMenuEntry", menuName = "DragAndDrop/ContextMenu/Built-in/Debug Entry", order = 101)]
    public class DebugContextMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        public override bool CanShow(ContextMenuContext ctx)
        {
            if (ctx.Inventory == null || ctx.BaseSlot == null || ctx.ItemAdapter == null)
                return false;
            return true;
        }

        public override void Execute(ContextMenuContext ctx)
        {
            Debug.Log($"_PrimaryAdapter name: {ctx.BaseSlot.Stack.DisplayName}, stack size: {ctx.BaseSlot.Stack.Count}");
        }
    }
}