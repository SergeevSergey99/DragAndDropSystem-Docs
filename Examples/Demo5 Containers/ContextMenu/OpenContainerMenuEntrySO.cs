using DragAndDropSystem.ContextMenu;
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
            return ctx.Item is ContainerItemAdapter adapter && adapter.Instance.IsContainer;
        }

        public override void Execute(ContextMenuContext ctx)
        {
            if (ctx.Item is not ContainerItemAdapter adapter)
                return;

            var controller = Object.FindFirstObjectByType<ContainerUIController>();
            if (controller == null)
            {
                Debug.LogWarning("[OpenContainerMenuEntry] ContainerUIController not found in scene");
                return;
            }

            controller.OpenContainer(adapter.Instance);
        }
    }
}
