using System;

namespace DragAndDropSystem.Examples.Containers
{
    public static class Events
    {
        public static event Action<ContainerItemInstance> OnOpenClick;
        
        public static void InvokeOpenClick(ContainerItemInstance instance) => OnOpenClick?.Invoke(instance);
    }
}