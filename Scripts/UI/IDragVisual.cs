using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.UI
{
    /// <summary>
    /// Interface for drag item visualization
    /// </summary>
    public interface IDragVisual
    {
        /// <summary>
        /// Show the visual using the specified entries
        /// </summary>
        void Show(IReadOnlyList<DragEntry> entries);

        /// <summary>
        /// Hide the visual
        /// </summary>
        void Hide();

        /// <summary>
        /// Update the visual position
        /// </summary>
        void UpdatePosition(Vector3 position);

        /// <summary>
        /// Check whether the visual is active
        /// </summary>
        bool IsVisible { get; }
    }
}