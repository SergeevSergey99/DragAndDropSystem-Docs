namespace UniversalDragAndDrop.Core
{
    /// <summary>
    /// Interface for items that support filtering.
    /// Items can implement this interface to provide filtering data.
    /// </summary>
    public interface IFilterable
    {
        /// <summary>
        /// Item category for filtering (for example: "Weapon", "Armor", "Consumable")
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Item subcategory (for example: "Sword", "Bow", "Staff" for the "Weapon" category)
        /// </summary>
        string Subcategory { get; }

        /// <summary>
        /// Item rarity for filtering (for example: 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary)
        /// </summary>
        int Rarity { get; }
    }

    /// <summary>
    /// Interface for items that support sorting.
    /// </summary>
    public interface ISortable
    {
        /// <summary>
        /// Sort value (for example: price, level, weight)
        /// </summary>
        int SortValue { get; }

        /// <summary>
        /// Name used for alphabetical sorting
        /// </summary>
        string SortName { get; }
    }
}