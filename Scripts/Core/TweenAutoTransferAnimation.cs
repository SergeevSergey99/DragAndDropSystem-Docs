using System;
using System.Collections.Generic;
using DG.Tweening;
using DragAndDropSystem.Slots;
using DragAndDropSystem.UI;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Анимация автопереноса с использованием DOTween
    /// Визуал летит от источника к цели, целевой слот обновляется только после завершения анимации
    /// </summary>
    [Serializable]
    public class TweenAutoTransferAnimation : AutoTransferAnimationStrategy
    {
        [Header("Animation Settings")]
        [SerializeField, Range(0.1f, 2f), Tooltip("Длительность анимации перелета")]
        private float _duration = 0.3f;

        [SerializeField, Tooltip("Тип easing для анимации")]
        private Ease _ease = Ease.OutCubic;

        [SerializeField, Tooltip("Использовать arc (дугу) при полете")]
        private bool _useArc = false;

        [SerializeField, Range(0f, 500f), Tooltip("Высота дуги в пикселях Canvas (если useArc = true)")]
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

            // Создаем анимацию
            Tween tween;

            if (_useArc)
            {
                // Конвертируем пиксели в мировые координаты с учетом Canvas scale
                float worldArcHeight = ConvertPixelsToWorldHeight(_arcHeightPixels, canvas);

                // Анимация с дугой через промежуточную точку
                Vector3 midPoint = (startPos + endPos) / 2f;
                midPoint.y += worldArcHeight;

                Vector3[] path = new[] { startPos, midPoint, endPos };

                tween = visualRect.DOPath(path, _duration, PathType.CatmullRom)
                    .SetEase(_ease);
            }
            else
            {
                // Простая линейная анимация
                tween = visualRect.DOMove(endPos, _duration)
                    .SetEase(_ease);
            }

            // Колбэк по завершении
            tween.OnComplete(() =>
            {
                // Скрываем и уничтожаем визуал
                dragVisual.Hide();
                UnityEngine.Object.Destroy(visualInstance.gameObject);

                // Вызываем колбэк завершения (обновит визуал целевого слота)
                onComplete?.Invoke();
            });

            // На случай если анимация будет убита
            tween.OnKill(() =>
            {
                if (visualInstance != null)
                {
                    dragVisual.Hide();
                    UnityEngine.Object.Destroy(visualInstance.gameObject);
                }
            });

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
    }
}
