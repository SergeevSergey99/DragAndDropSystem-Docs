using DragAndDropSystem.ContextMenu;
using DragAndDropSystem.Examples.Containers.UI;
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
            return ctx.ItemAdapter is ContainerItemAdapterAdapter { Instance: ContainerItemInstance };
        }

        public override void Execute(ContextMenuContext ctx)
        {
            if (ctx.ItemAdapter is not ContainerItemAdapterAdapter adapter)
                return;
            
            if (adapter.Instance is ContainerItemInstance  instance)
                Events.InvokeOpenClick(instance);
        }
    }
}
