namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Interface for handling drop operations.
    /// Decouples drop handling from IInventory, allowing non-inventory targets
    /// like WorldDropZone to handle drops without fake inventory wrappers.
    /// Named IDropProcessor to avoid conflict with Unity's IDropHandler.
    /// </summary>
    public interface IDropProcessor
    {
        /// <summary>
        /// Check if this processor can accept the drop based on the current drag context.
        /// Called during hover to determine visual feedback and on drop to validate.
        /// </summary>
        /// <param name="context">The current drag context with source and target information</param>
        /// <returns>True if the drop can be accepted</returns>
        bool CanAcceptDrop(DragContext context);

        /// <summary>
        /// Execute the drop operation.
        /// Called after CanAcceptDrop returns true and the user releases the mouse.
        /// </summary>
        /// <param name="context">The current drag context</param>
        /// <returns>Result of the drop operation</returns>
        DropResult ProcessDrop(DragContext context);
    }
}
