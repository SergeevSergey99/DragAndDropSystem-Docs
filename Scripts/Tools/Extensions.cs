using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UDND.Tools
{
    public static class Extensions
    {
        /// <summary>
        /// Observe a fire-and-forget task so its exceptions reach the console.
        /// An unobserved faulted Task is silent in Unity, which turns a crashed async drag
        /// operation into a hang with an empty log.
        /// </summary>
        public static void LogFaults(this Task task)
        {
            if (task == null)
                return;

            task.ContinueWith(
                completed => Debug.LogException(completed.Exception),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <inheritdoc cref="LogFaults(Task)"/>
        public static void LogFaults<T>(this Task<T> task)
        {
            LogFaults((Task)task);
        }

        public static void DragAndDropLog(string message)
        {
#if UNITY_EDITOR && UDND_LOG
            Debug.Log($"<color=cyan>[UDND]</color> {message}");
#endif
        }
        public static void DragAndDropLogWarning(string message)
        {
#if UNITY_EDITOR && UDND_LOG
            Debug.LogWarning($"<color=cyan>[UDND]</color> {message}");
#endif
        }

        public static bool TryGetSlotBoundsInParent(RectTransform slotRect, RectTransform parentRT, Camera targetCamera, out Vector2 centerLocal, out float halfWidthLocal, out Vector2 centerScreenPos)
        {
            centerLocal = default;
            halfWidthLocal = 0f;
            centerScreenPos = default;

            if (slotRect == null)
                return false;

            Camera sourceCamera = GetCanvasCamera(slotRect);
            GetRectBoundsInParent(slotRect, parentRT, sourceCamera, targetCamera, out var minLocal, out var maxLocal);

            centerLocal = (minLocal + maxLocal) * 0.5f;
            halfWidthLocal = (maxLocal.x - minLocal.x) * 0.5f;

            var worldCenter = slotRect.TransformPoint(slotRect.rect.center);
            centerScreenPos = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldCenter);
            return true;
        }

        public static void GetRectBoundsInParent(RectTransform rect, RectTransform parentRT, Camera sourceCamera, Camera targetCamera, out Vector2 minLocal, out Vector2 maxLocal)
        {
            var worldCorners = new Vector3[4];
            rect.GetWorldCorners(worldCorners);

            minLocal = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            maxLocal = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPoint, targetCamera, out var localPoint))
                    continue;

                minLocal = Vector2.Min(minLocal, localPoint);
                maxLocal = Vector2.Max(maxLocal, localPoint);
            }

            if (float.IsInfinity(minLocal.x) || float.IsInfinity(maxLocal.x))
            {
                minLocal = Vector2.zero;
                maxLocal = new Vector2(rect.rect.width, rect.rect.height);
            }
        }

        public static Camera GetCanvasCamera(Component component)
        {
            var canvas = component != null ? component.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                return null;

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }
    }
}