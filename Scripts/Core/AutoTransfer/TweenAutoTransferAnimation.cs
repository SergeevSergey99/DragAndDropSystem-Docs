using System;
using UnityEngine;
using UDND.Slots;
using UDND.Tools;
using UDND.UI;

namespace UDND.Core
{
    /// <summary>
    /// Auto-transfer animation built on the lightweight tween runner
    /// The visual moves from source to target, and the target slot is updated only after the animation completes
    /// </summary>
    [Serializable]
    public class TweenAutoTransferAnimation : AutoTransferAnimationStrategy
    {
        [Header("Animation Settings")]
        [SerializeField, Range(0.1f, 2f), Tooltip("Flight animation duration")]
        private float _duration = 0.3f;

        [SerializeField, Tooltip("Animation easing type")]
        private MiniTweenEase _ease = MiniTweenEase.OutCubic;

        [SerializeField, Tooltip("Use an arc during flight")]
        private bool _useArc = false;

        [SerializeField, Range(0f, 500f), Tooltip("Arc height in Canvas pixels (if useArc = true)")]
        private float _arcHeightPixels = 50f;

        public override GameObject AnimateTransfer(
            ItemStack stack,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            MonoBehaviour visualPrefab,
            Transform visualContainer,
            Canvas canvas,
            Action onComplete)
        {
            if (sourceBaseSlot == null || targetBaseSlot == null || visualPrefab == null)
            {
                Debug.LogWarning("TweenAutoTransferAnimation: Invalid parameters, falling back to instant");
                onComplete?.Invoke();
                return null;
            }

            // Create the visual instance
            var visualInstance = UnityEngine.Object.Instantiate(visualPrefab, visualContainer);

            if (!(visualInstance is BaseDragVisual dragVisual))
            {
                Debug.LogError("TweenAutoTransferAnimation: Visual prefab doesn't implement IDragVisual!");
                UnityEngine.Object.Destroy(visualInstance.gameObject);
                onComplete?.Invoke();
                return null;
            }

            // Get the visual RectTransform
            var visualRect = visualInstance.transform as RectTransform;
            if (visualRect == null)
            {
                Debug.LogError("TweenAutoTransferAnimation: Visual doesn't have RectTransform!");
                UnityEngine.Object.Destroy(visualInstance.gameObject);
                onComplete?.Invoke();
                return null;
            }

            // Get slot world positions
            Vector3 startPos = GetSlotWorldPosition(sourceBaseSlot);
            Vector3 endPos = GetSlotWorldPosition(targetBaseSlot);

            // Set the initial position
            visualRect.position = startPos;

            // Show the visual with the item
            dragVisual.Show(new DragEntry(stack, sourceBaseSlot, null));

            Func<float, Vector3> customPath = null;
            if (_useArc)
            {
                float worldArcHeight = ConvertPixelsToWorldHeight(_arcHeightPixels, canvas);
                Vector3 midPoint = (startPos + endPos) * 0.5f;
                midPoint.y += worldArcHeight;
                customPath = t => EvaluateQuadraticBezier(startPos, midPoint, endPos, t);
            }

            MiniTweenRunner.AutoCreateInstance.AnimatePosition(
                visualRect,
                startPos,
                endPos,
                _duration,
                _ease,
                (_, position) => visualRect.position = position,
                onComplete: () =>
                {
                    dragVisual.Hide();
                    UnityEngine.Object.Destroy(visualInstance.gameObject);
                    onComplete?.Invoke();
                },
                onInterrupted: () =>
                {
                    if (visualInstance != null)
                    {
                        dragVisual.Hide();
                        UnityEngine.Object.Destroy(visualInstance.gameObject);
                    }
                },
                customPath: customPath);

            // Return the visual GameObject for tracking
            return visualInstance.gameObject;
        }

        /// <summary>
        /// Get the world position of a slot for animation
        /// </summary>
        private Vector3 GetSlotWorldPosition(BaseSlot baseSlot)
        {
            if (baseSlot?.Transform == null)
            {
                Debug.LogWarning("GetSlotWorldPosition: Slot or Transform is null");
                return Vector3.zero;
            }

            // For RectTransform use position (world position including canvas transform)
            if (baseSlot.Transform is RectTransform rectTransform)
            {
                return rectTransform.position;
            }

            // Fallback to a regular Transform
            return baseSlot.Transform.position;
        }

        /// <summary>
        /// Convert canvas pixels into world coordinates along the height axis
        /// </summary>
        private float ConvertPixelsToWorldHeight(float pixels, Canvas canvas)
        {
            if (canvas == null)
                return pixels;

            var canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null)
                return pixels;

            // Create two points in Canvas local coordinates with a difference in pixels
            Vector2 localPoint1 = Vector2.zero;
            Vector2 localPoint2 = new Vector2(0, pixels);

            // Convert them to world coordinates
            Vector3 worldPoint1 = canvasRect.TransformPoint(localPoint1);
            Vector3 worldPoint2 = canvasRect.TransformPoint(localPoint2);

            // Compute the world-space difference
            float worldHeight = worldPoint2.y - worldPoint1.y;

            return worldHeight;
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float inverseT = 1f - t;
            return (inverseT * inverseT * start) +
                   (2f * inverseT * t * control) +
                   (t * t * end);
        }
    }
}