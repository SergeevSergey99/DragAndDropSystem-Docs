using UnityEngine;

namespace DragAndDropSystem.Tools
{
    public static class Extensions
    {
        public static void DragAndDropLog(string message)
        {
#if UNITY_EDITOR && UNIVERSAL_INVENTORY_LOG
            Debug.Log($"<color=cyan>[DragAndDropSystem]</color> {message}");
#endif
        }
    }
}