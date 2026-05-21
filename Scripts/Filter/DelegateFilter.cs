namespace UDND.Filter
{
    /// <summary>
    /// Wraps a <see cref="FilterPredicate"/> delegate as <see cref="ISlotFilter"/>.
    /// Created internally by <see cref="FilterSortController"/> for lambda-based API.
    /// </summary>
    internal sealed class DelegateFilter : ISlotFilter
    {
        private readonly FilterPredicate _predicate;

        public DelegateFilter(FilterPredicate predicate) => _predicate = predicate;

        public bool Evaluate(in FilterContext ctx) => _predicate(in ctx);
    }

    /// <summary>
    /// Wraps a <see cref="SortComparison"/> delegate as <see cref="ISlotSorter"/>.
    /// Created internally by <see cref="FilterSortController"/> for lambda-based API.
    /// </summary>
    internal sealed class DelegateSorter : ISlotSorter
    {
        private readonly SortComparison _comparison;

        public DelegateSorter(SortComparison comparison) => _comparison = comparison;

        public int Compare(in FilterContext a, in FilterContext b) => _comparison(in a, in b);
    }
}
