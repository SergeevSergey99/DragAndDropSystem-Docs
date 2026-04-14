using DragAndDropSystem.ContextMenu;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// "Open" context menu entry for container items.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/Open Container Menu Entry")]
    public class OpenContainerMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        
        public override bool CanShow(ContextMenuContext ctx)
        {
            return ctx.ItemStack?.PrimaryAdapter is ContainerItemAdapterAdapter { Instance: ContainerItemInstance };
        }

        public override void Execute(ContextMenuContext ctx)
        {
            if (ctx.ItemStack?.PrimaryAdapter is not ContainerItemAdapterAdapter adapter)
                return;
            
            if (adapter.Instance is ContainerItemInstance  instance)
                Events.InvokeOpenClick(instance);
        }
    }
}