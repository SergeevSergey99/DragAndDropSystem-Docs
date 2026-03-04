using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Interface for objects that can be drop targets.
    /// Implemented by components that accept drag-and-drop (slots, areas, world drop zones, etc.)
    /// </summary>
    public interface IDropTarget
    {
        /// <summary>
        /// Get the target slot (can be null for areas like InventoryDropArea or WorldDropZone)
        /// </summary>
        ISlot GetTargetSlot();

        /// <summary>
        /// Get the drop processor responsible for validating and executing drops on this target.
        /// The processor encapsulates all drop logic, including validation and item transfer.
        /// </summary>
        IDropProcessor GetDropHandler();

        /// <summary>
        /// Called when this target becomes active (top of the target stack).
        /// Used for visual highlighting.
        /// </summary>
        void OnBecomeActiveTarget();

        /// <summary>
        /// Called when this target stops being active.
        /// Used to remove visual highlighting.
        /// </summary>
        void OnBecomeInactiveTarget();
    }
}
