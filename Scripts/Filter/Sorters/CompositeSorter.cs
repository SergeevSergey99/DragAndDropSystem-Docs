using System;
using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Filter.Sorters
{
    /// <summary>
    /// Chains multiple <see cref="ISlotSorter"/>: uses the first non-zero result.
    /// </summary>
    [Serializable]
    public class CompositeSorter : ISlotSorter
    {
        [SerializeReference, ManagedReferencePicker]
        private List<ISlotSorter> _sorters = new List<ISlotSorter>();

        public IList<ISlotSorter> Sorters => _sorters;

        public void Add(ISlotSorter sorter) => _sorters.Add(sorter);
        public void Remove(ISlotSorter sorter) => _sorters.Remove(sorter);
        public void Clear() => _sorters.Clear();

        public int Compare(in FilterContext a, in FilterContext b)
        {
            foreach (var sorter in _sorters)
            {
                if (sorter == null) continue;
                int result = sorter.Compare(in a, in b);
                if (result != 0) return result;
            }
            return 0;
        }
    }
}
