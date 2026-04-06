using System;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using DragAndDropSystem.UI;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Анимация автопереноса на встроенном lightweight tween runner
    /// Визуал летит от источника к цели, целевой слот обновляется только после завершения анимации
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
            ISlot sourceSlot,
            ISlot targetSlot,
            MonoBehaviour visualPrefab,
            Transform visualContainer,
            Canvas canvas,
            Action onComplete)
        {
            if (sourceSlot == null || targetSlot == null || visualPrefab == null)
            {
                Debug.LogWarning("TweenAutoTransferAnimation: Invalid parameters, falling back to instant");
                onComplete?.Invoke();
                return null;
            }

            // Создаем экземпляр визуала
            var visualInstance = UnityEngine.Object.Instantiate(visualPrefab, visualContainer);

            if (!(visualInstance is IDragVisual dragVisual))
            {
                Debug.LogError("TweenAutoTransferAnimation: Visual prefab doesn't implement IDragVisual!");
                UnityEngine.Object.Destroy(visualInstance.gameObject);
                onComplete?.Invoke();
                return null;
            }

            // Получаем RectTransform визуала
            var visualRect = visualInstance.transform as RectTransform;
            if (visualRect == null)
            {
                Debug.LogError("TweenAutoTransferAnimation: Visual doesn't have RectTransform!");
                UnityEngine.Object.Destroy(visualInstance.gameObject);
                onComplete?.Invoke();
                return null;
            }

            // Получаем мировые позиции слотов
            Vector3 startPos = GetSlotWorldPosition(sourceSlot);
            Vector3 endPos = GetSlotWorldPosition(targetSlot);

            // Устанавливаем начальную позицию
            visualRect.position = startPos;

            // Показываем визуал с предметом
            // Create a temporary DragEntry for the visual
            var entries = new[] { new DragEntry(stack, sourceSlot, null) };
            dragVisual.Show(entries);

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

            // Возвращаем GameObject визуала для отслеживания
            return visualInstance.gameObject;
        }

        /// <summary>
        /// Получить мировую позицию слота для анимации
        /// </summary>
        private Vector3 GetSlotWorldPosition(ISlot slot)
        {
            if (slot?.Transform == null)
            {
                Debug.LogWarning("GetSlotWorldPosition: Slot or Transform is null");
                return Vector3.zero;
            }

            // Для RectTransform используем position (мировая позиция с учетом canvas)
            if (slot.Transform is RectTransform rectTransform)
            {
                return rectTransform.position;
            }

            // Fallback на обычный Transform
            return slot.Transform.position;
        }

        /// <summary>
        /// Конвертировать пиксели Canvas в мировые координаты по высоте
        /// </summary>
        private float ConvertPixelsToWorldHeight(float pixels, Canvas canvas)
        {
            if (canvas == null)
                return pixels;

            var canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null)
                return pixels;

            // Создаем две точки в локальных координатах Canvas с разницей в pixels
            Vector2 localPoint1 = Vector2.zero;
            Vector2 localPoint2 = new Vector2(0, pixels);

            // Конвертируем в мировые координаты
            Vector3 worldPoint1 = canvasRect.TransformPoint(localPoint1);
            Vector3 worldPoint2 = canvasRect.TransformPoint(localPoint2);

            // Вычисляем разницу в мировых координатах
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
