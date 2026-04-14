namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// Core filter contract. Determines whether a slot should be visible.
    /// Implement as [Serializable] class, ScriptableObject, or MonoBehaviour.
    /// </summary>
    public interface ISlotFilter
    {
        bool Evaluate(in FilterContext ctx);
    }

    /// <summary>
    /// Core sorter contract. Compares two slots for ordering.
    /// Implement as [Serializable] class, ScriptableObject, or MonoBehaviour.
    /// </summary>
    public interface ISlotSorter
    {
        int Compare(in FilterContext a, in FilterContext b);
    }

    /// <summary>Filter delegate for inline / lambda usage.</summary>
    public delegate bool FilterPredicate(in FilterContext ctx);

    /// <summary>Sort delegate for inline / lambda usage.</summary>
    public delegate int SortComparison(in FilterContext a, in FilterContext b);
}
