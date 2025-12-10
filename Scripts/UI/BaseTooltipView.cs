using System;
using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Интерфейс для визуализации tooltip предмета.
    /// Аналог IDragVisual - позволяет создавать разные визуальные представления tooltip.
    /// </summary>
    public abstract class BaseTooltipView : MonoBehaviour
    {
        public RectTransform rectTransform => transform as RectTransform;

        /// <summary>
        /// Показать tooltip с указанным предметом
        /// </summary>
        /// <param name="item">Предмет для отображения</param>
        public virtual void Show(IInventoryItem item, Action OnCompleted = null)
        {
            gameObject.SetActive(true);
            OnCompleted?.Invoke();
        }

        /// <summary>
        /// Скрыть tooltip
        /// </summary>
        public virtual void Hide(Action OnCompleted = null)
        {
            gameObject.SetActive(false);
            OnCompleted?.Invoke();
        }

        /// <summary>
        /// Обновить позицию tooltip
        /// </summary>
        /// <param name="position">Новая позиция в экранных координатах</param>
        public virtual void UpdatePosition(Vector2 position){ transform.position = position; }

        /// <summary>
        /// Обновить содержимое tooltip (если предмет изменился но tooltip все еще показывается)
        /// </summary>
        /// <param name="item">Обновленный предмет</param>
        public abstract void SetContent(IInventoryItem item);

        /// <summary>
        /// Получить размер tooltip
        /// </summary>
        public virtual Vector2 GetSize() => rectTransform.rect.size;
    }
}
