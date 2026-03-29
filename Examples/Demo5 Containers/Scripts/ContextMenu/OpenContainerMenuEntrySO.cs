using DragAndDropSystem.ContextMenu;
using DragAndDropSystem.Examples.Containers.UI;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Пункт контекстного меню "Открыть" для предметов-контейнеров.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/Open Container Menu Entry")]
    public class OpenContainerMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        
        public override bool CanShow(ContextMenuContext ctx)
        {
            return ctx.Item is ContainerItemAdapter { Instance: ContainerItemInstance };
        }

        public override void Execute(ContextMenuContext ctx)
        {
            if (ctx.Item is not ContainerItemAdapter adapter)
                return;
            
            if (adapter.Instance is ContainerItemInstance  instance)
                Events.InvokeOpenClick(instance);
        }
    }
}
