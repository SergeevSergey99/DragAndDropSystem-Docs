using System;
using System.Collections.Generic;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Filter.Filters
{
    /// <summary>
    /// Combines multiple <see cref="ISlotFilter"/> with AND/OR logic.
    /// </summary>
    [Serializable]
    public class CompositeFilter : ISlotFilter
    {
        public enum Mode { All, Any }

        [field: SerializeField] public Mode CompositionMode { get; set; } = Mode.All;

        [SerializeReference, ManagedReferencePicker]
        private List<ISlotFilter> _filters = new List<ISlotFilter>();

        public IList<ISlotFilter> Filters => _filters;

        public void Add(ISlotFilter filter) => _filters.Add(filter);
        public void Remove(ISlotFilter filter) => _filters.Remove(filter);
        public void Clear() => _filters.Clear();

        public bool Evaluate(in FilterContext ctx)
        {
            if (_filters == null || _filters.Count == 0) return true;

            if (CompositionMode == Mode.All)
            {
                foreach (var f in _filters)
                {
                    if (f != null && !f.Evaluate(in ctx)) return false;
                }
                return true;
            }
            else
            {
                foreach (var f in _filters)
                {
                    if (f != null && f.Evaluate(in ctx)) return true;
                }
                return false;
            }
        }
    }
}
