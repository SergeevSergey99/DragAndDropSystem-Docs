using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;

namespace UDND.DataBinding
{
    /// <summary>
    /// Serializable placement payload used by placement-aware inventory bindings.
    /// A placement is described by the full list of item instances it holds, so stacks made of
    /// distinct adapter instances (for example, items carrying per-instance state) are preserved
    /// without data loss.
    /// </summary>
    public readonly struct PlacementData<TData>
    {
        private readonly IReadOnlyList<TData> _items;

        /// <summary>
        /// Placement made of distinct item instances. Preserves every instance in the stack.
        /// </summary>
        public PlacementData(
            IReadOnlyList<TData> items,
            int anchorIndex,
            int orientation = 0)
        {
            _items = items;
            AnchorIndex = anchorIndex;
            Orientation = orientation;
        }

        /// <summary>
        /// Convenience for homogeneous placements: a single item repeated <paramref name="count"/> times.
        /// Use the list constructor when each item instance must be preserved individually.
        /// </summary>
        public PlacementData(
            TData item,
            int anchorIndex,
            int count = 1,
            int orientation = 0)
        {
            var items = new TData[Math.Max(1, count)];
            for (int i = 0; i < items.Length; i++)
                items[i] = item;

            _items = items;
            AnchorIndex = anchorIndex;
            Orientation = orientation;
        }

        /// <summary>All item instances stored at this placement.</summary>
        public IReadOnlyList<TData> Items => _items ?? Array.Empty<TData>();

        /// <summary>First item instance, for single-item placements.</summary>
        public TData Item => Items.Count > 0 ? Items[0] : default;

        public int AnchorIndex { get; }
        public int Count => Items.Count;
        public int Orientation { get; }
    }

    /// <summary>
    /// Context passed when a placement-aware binding commits a UI change back to external data.
    /// Exposes every adapter (and its extracted data) in the committed stack so heterogeneous or
    /// per-instance stacks are not collapsed to a single primary item.
    /// </summary>
    public readonly struct PlacementCommitContext<TData, TAdapter>
        where TAdapter : class, IItemAdapter
    {
        public PlacementCommitContext(
            InventoryItemEventContext eventContext,
            IReadOnlyList<TData> data,
            IReadOnlyList<TAdapter> adapters)
        {
            EventContext = eventContext;
            Data = data ?? Array.Empty<TData>();
            Adapters = adapters ?? Array.Empty<TAdapter>();
        }

        public InventoryItemEventContext EventContext { get; }

        /// <summary>Extracted data for every adapter in the committed stack.</summary>
        public IReadOnlyList<TData> Data { get; }

        /// <summary>Every typed adapter in the committed stack.</summary>
        public IReadOnlyList<TAdapter> Adapters { get; }

        /// <summary>First adapter, for single-item placements.</summary>
        public TAdapter Adapter => Adapters.Count > 0 ? Adapters[0] : null;

        /// <summary>First extracted data item, for single-item placements.</summary>
        public TData PrimaryData => Data.Count > 0 ? Data[0] : default;

        public ItemStack Stack => EventContext?.Stack ?? ItemStack.Empty();
        public int Count => Stack.Count;
        public int AnchorIndex => EventContext?.AnchorIndex ?? -1;
        public int Orientation => EventContext?.Orientation ?? 0;
        public Vector2Int BoundingSize => EventContext?.BoundingSize ?? Vector2Int.one;
        public PlacementSnapshot PlacementSnapshot => EventContext?.PlacementSnapshot;
        public IReadOnlyList<int> CoveredIndices => EventContext?.CoveredIndices ?? Array.Empty<int>();
        public IReadOnlyList<Vector2Int> CoveredOffsets => EventContext?.CoveredOffsets ?? Array.Empty<Vector2Int>();
    }

    /// <summary>
    /// DataBinding template for inventories that persist anchor/orientation placement data.
    /// Slot inventories use the same data shape and collapse placements into one slot.
    /// Each placement is described by its full list of item instances, so stacks made of distinct
    /// adapter instances are preserved without data loss.
    /// </summary>
    public abstract class PlacementInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        protected abstract IEnumerable<PlacementData<TData>> GetPlacements();
        protected abstract TAdapter CreateAdapter(TData item);
        protected abstract TData ExtractData(TAdapter adapter);
        protected abstract void AddPlacementData(PlacementCommitContext<TData, TAdapter> context);
        protected abstract void RemovePlacementData(PlacementCommitContext<TData, TAdapter> context);

        protected override void OnReloadUI()
        {
            var placements = GetPlacements();
            if (placements == null || Inventory == null)
                return;

            if (Inventory is not IPlacementInventory placementInventory)
                return;

            foreach (var placement in placements)
                ReloadPlacement(placement, placementInventory);
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (!TryCollectCommit(context, out var commit))
                return;

            AddPlacementData(commit);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (!TryCollectCommit(context, out var commit))
                return;

            RemovePlacementData(commit);
        }

        private bool TryCollectCommit(
            InventoryItemEventContext context,
            out PlacementCommitContext<TData, TAdapter> commit)
        {
            commit = default;

            var stack = context?.Stack;
            if (stack?.Adapters == null)
                return false;

            var adapters = new List<TAdapter>(stack.Adapters.Count);
            var data = new List<TData>(stack.Adapters.Count);
            for (int i = 0; i < stack.Adapters.Count; i++)
            {
                if (stack.Adapters[i] is TAdapter typed)
                {
                    adapters.Add(typed);
                    data.Add(ExtractData(typed));
                }
            }

            if (adapters.Count == 0)
                return false;

            commit = new PlacementCommitContext<TData, TAdapter>(context, data, adapters);
            return true;
        }

        private void ReloadPlacement(PlacementData<TData> placement, IPlacementInventory placementInventory)
        {
            var items = placement.Items;
            if (items.Count == 0)
                return;

            if (placement.AnchorIndex >= 0 && TryCreateStack(items, out var stack))
            {
                var request = new PlacementRequest(
                    stack,
                    placement.AnchorIndex,
                    placement.Orientation,
                    PlacementShapeUtility.Resolve(stack.PrimaryAdapter));

                if (!placementInventory.TryPlace(request, out _))
                    OnPlacementReloadFailed(placement, stack.PrimaryAdapter);

                return;
            }

            var adapters = CreateAdapters(items);
            if (adapters.Count > 0)
                AddToUIQuiet(adapters, -1);
        }

        private bool TryCreateStack(IReadOnlyList<TData> items, out ItemStack stack)
        {
            stack = ItemStack.Empty();
            var adapters = CreateAdapters(items);
            if (adapters.Count == 0)
                return false;

            return ItemStack.TryCreate(adapters, out stack);
        }

        private List<IItemAdapter> CreateAdapters(IReadOnlyList<TData> items)
        {
            var adapters = new List<IItemAdapter>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var adapter = CreateAdapter(items[i]);
                if (adapter != null)
                    adapters.Add(adapter);
            }

            return adapters;
        }

        protected virtual void OnPlacementReloadFailed(PlacementData<TData> placement, IItemAdapter adapter)
        {
            Debug.LogWarning(
                $"[{GetType().Name}] Could not place '{adapter?.DisplayName ?? "item"}' at anchor {placement.AnchorIndex}.",
                this);
        }
    }
}
