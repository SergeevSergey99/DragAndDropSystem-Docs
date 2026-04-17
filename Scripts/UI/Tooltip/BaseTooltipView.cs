using System;
using UnityEngine;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.UI
{
    /// <summary>
    /// Base abstraction for item tooltip visualization.
    /// Similar to IDragVisual, it allows creating different tooltip presentations.
    /// </summary>
    public abstract class BaseTooltipView : MonoBehaviour
    {
        public RectTransform rectTransform => transform as RectTransform;

        /// <summary>
        /// Show the tooltip for the specified item
        /// </summary>
        /// <param name="itemAdapter">Item to display</param>
        public virtual void Show(IItemAdapter itemAdapter, Action OnCompleted = null)
        {
            if (itemAdapter == null)
            {
                Hide(OnCompleted);
                return;
            }

            SetContent(itemAdapter);
            ShowView(OnCompleted);
        }

        /// <summary>
        /// Hide the tooltip
        /// </summary>
        public virtual void Hide(Action OnCompleted = null)
        {
            HideView(OnCompleted);
        }

        /// <summary>
        /// Update tooltip position
        /// </summary>
        /// <param name="position">New position in screen coordinates</param>
        public virtual void UpdatePosition(Vector2 position){ transform.position = position; }

        /// <summary>
        /// Update tooltip content (if the item changed while the tooltip is still visible)
        /// </summary>
        /// <param name="itemAdapter">Updated item</param>
        protected abstract void SetContent(IItemAdapter itemAdapter);

        /// <summary>
        /// Get tooltip size
        /// </summary>
        public virtual Vector2 GetSize() => rectTransform.rect.size;

        protected virtual void ShowView(Action OnCompleted)
        {
            gameObject.SetActive(true);
            OnCompleted?.Invoke();
        }

        protected virtual void HideView(Action OnCompleted)
        {
            gameObject.SetActive(false);
            OnCompleted?.Invoke();
        }
    }
}
