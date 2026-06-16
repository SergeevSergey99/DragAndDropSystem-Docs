using System;
using UnityEngine;

namespace UDND.Examples.Containers
{
    public static class Events
    {
        public static event Action<ContainerItemInstance> OnOpenClick;
        public static event Action<ContainerItemInstance> OnContainerContentChanged;

        public static void InvokeOpenClick(ContainerItemInstance instance) => OnOpenClick?.Invoke(instance);
        public static void InvokeContainerContentChanged(ContainerItemInstance instance) => OnContainerContentChanged?.Invoke(instance);

        // "Fast Enter Play Mode" (no domain reload) keeps static event subscribers across Play
        // sessions; clear them at session start so destroyed listeners are never invoked.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnOpenClick = null;
            OnContainerContentChanged = null;
        }
    }
}