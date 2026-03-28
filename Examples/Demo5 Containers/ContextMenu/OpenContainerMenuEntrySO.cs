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

            var controller = FindFirstObjectByType<ContainerUIController>();
            if (controller == null)
            {
                Debug.LogWarning("[OpenContainerMenuEntry] ContainerUIController not found in scene");
                return;
            }
            
            var instance = adapter.Instance as ContainerItemInstance;

            controller.OpenContainer(instance);
        }
    }
}
