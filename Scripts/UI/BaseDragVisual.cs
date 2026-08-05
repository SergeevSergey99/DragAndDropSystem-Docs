using System.Collections.Generic;
using UnityEngine;
using UDND.Core;

namespace UDND.UI
{
    /// <summary>
    /// Interface for drag item visualization
    /// </summary>
    public abstract class BaseDragVisual : MonoBehaviour
    {
        protected RectTransform _rectTransform => transform as RectTransform;
        
        /// <summary>
        /// Show the visual using the specified entry
        /// </summary>
        public abstract void Show(DragEntry entry);

        /// <summary>
        /// Hide the visual
        /// </summary>
        public abstract void Hide();

        /// <summary>
        /// Update the visual position
        /// </summary>
        public virtual void UpdatePosition(Vector3 position)
        {
            if (_rectTransform != null)
            {
                _rectTransform.position = position;
            }
        }
    }
}