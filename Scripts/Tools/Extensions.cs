using UnityEngine;

namespace DragAndDropSystem.Tools
{
    public static class Extensions
    {
        public static void DragAndDropLog(string message)
        {
            //return;
            Debug.Log($"<color=cyan>[DragAndDropSystem]</color> {message}");
        }
    }
}