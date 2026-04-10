namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Optional interface for items that define their own stack limit.
    /// Implement it on the adapter together with IItemAdapter to specify
    /// the maximum stack size for a specific item type.
    /// Used only if the strategy/inventory allows item override.
    /// If the interface is not implemented or override is disabled, the inventory/strategy limit is used instead.
    /// </summary>
    public interface IStackSizeLimitable
    {
        /// <summary>
        /// Maximum number of items of this type in a single stack
        /// </summary>
        int MaxStackSize { get; }
    }
}
