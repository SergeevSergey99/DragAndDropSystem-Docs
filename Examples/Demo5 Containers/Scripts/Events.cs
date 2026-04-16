using System;

namespace UniversalDragAndDrop.Examples.Containers
{
    public static class Events
    {
        public static event Action<ContainerItemInstance> OnOpenClick;
        public static event Action<ContainerItemInstance> OnContainerContentChanged;

        public static void InvokeOpenClick(ContainerItemInstance instance) => OnOpenClick?.Invoke(instance);
        public static void InvokeContainerContentChanged(ContainerItemInstance instance) => OnContainerContentChanged?.Invoke(instance);
    }
}