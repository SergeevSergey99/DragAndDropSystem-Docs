using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UDND.Core;

namespace UDND.Inventories
{
    /// <summary>
    /// Drag-scoped memo for cross-inventory adapter conversion.
    /// <para>
    /// A converted adapter depends only on the source inventory, the target inventory and the
    /// concrete source adapter instance, so the same object can serve every probe, every preview
    /// and the final mutation of one drag. Reusing it is not only an allocation win: the item the
    /// preview validated is then literally the item that lands in the target inventory.
    /// </para>
    /// <para>
    /// The session lives exactly as long as the <see cref="DragContext"/> that owns it and is
    /// shared by every context derived from it. It memoizes objects only — never rule verdicts,
    /// placement candidates or occupancy, which must always be recomputed against live state.
    /// </para>
    /// <para>
    /// Converters must therefore be pure factories for the duration of a drag: no registry
    /// writes, no id counters, no spawning. A conversion that never reaches a drop must leave
    /// no trace.
    /// </para>
    /// </summary>
    public sealed class TransferConversionSession
    {
        private readonly Dictionary<ConversionKey, IItemAdapter> _converted =
            new Dictionary<ConversionKey, IItemAdapter>(ConversionKeyComparer.Instance);

        /// <summary>
        /// Returns the converted adapter for this source instance, invoking <paramref name="convert"/>
        /// only on a miss. Failed conversions are not cached: they are cheap to recompute and a
        /// negative result may depend on inventory state that changes during the drag.
        /// </summary>
        public bool TryResolve(
            IInventory sourceInventory,
            IInventory targetInventory,
            IItemAdapter sourceItemAdapter,
            Func<IItemAdapter, IItemAdapter> convert,
            out IItemAdapter convertedItemAdapter)
        {
            convertedItemAdapter = null;
            if (sourceItemAdapter == null || convert == null)
                return false;

            var key = new ConversionKey(sourceInventory, targetInventory, sourceItemAdapter);
            if (_converted.TryGetValue(key, out var cached))
            {
                convertedItemAdapter = cached;
                return cached != null;
            }

            var converted = convert(sourceItemAdapter);
            if (converted == null)
                return false;

            _converted[key] = converted;
            convertedItemAdapter = converted;
            return true;
        }

        /// <summary>
        /// Drops the entry for a source adapter whose transfer has been committed: the converted
        /// object now belongs to the target inventory and must never be handed out as a candidate
        /// for another placement.
        /// </summary>
        public void Consume(IInventory sourceInventory, IInventory targetInventory, IItemAdapter sourceItemAdapter)
        {
            if (sourceItemAdapter == null)
                return;

            _converted.Remove(new ConversionKey(sourceInventory, targetInventory, sourceItemAdapter));
        }

        public void Clear() => _converted.Clear();

        private readonly struct ConversionKey
        {
            public readonly IInventory SourceInventory;
            public readonly IInventory TargetInventory;
            public readonly IItemAdapter SourceItemAdapter;

            public ConversionKey(IInventory sourceInventory, IInventory targetInventory, IItemAdapter sourceItemAdapter)
            {
                SourceInventory = sourceInventory;
                TargetInventory = targetInventory;
                SourceItemAdapter = sourceItemAdapter;
            }
        }

        /// <summary>
        /// Reference identity on every component. Value equality (for example by ItemId) would
        /// collapse distinct instances of a stack into one cache entry and hand the same object
        /// to several placements.
        /// </summary>
        private sealed class ConversionKeyComparer : IEqualityComparer<ConversionKey>
        {
            public static readonly ConversionKeyComparer Instance = new ConversionKeyComparer();

            public bool Equals(ConversionKey x, ConversionKey y)
            {
                return ReferenceEquals(x.SourceItemAdapter, y.SourceItemAdapter) &&
                       ReferenceEquals(x.SourceInventory, y.SourceInventory) &&
                       ReferenceEquals(x.TargetInventory, y.TargetInventory);
            }

            public int GetHashCode(ConversionKey obj)
            {
                unchecked
                {
                    int hash = RuntimeHelpers.GetHashCode(obj.SourceItemAdapter);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(obj.SourceInventory);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(obj.TargetInventory);
                    return hash;
                }
            }
        }
    }
}
