using UnityEngine;

namespace UniversalDragAndDrop.Core
{
    /// <summary>
    /// Base interface for any inventory item
    /// Minimal set of properties required by the system
    /// </summary>
    public interface IItemAdapter
    {
        /// <summary>
        /// Unique item identifier
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// Item icon used for display
        /// </summary>
        Sprite Icon { get; }

        /// <summary>
        /// Display name of the item
        /// </summary>
        string DisplayName { get; }
    }
}