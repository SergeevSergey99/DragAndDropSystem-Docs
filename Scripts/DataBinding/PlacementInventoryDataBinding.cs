using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;

namespace UDND.DataBinding
{
    /// <summary>
    /// Serializable placement payload used by placement-aware inventory bindings.
    /// </summary>
    public readonly struct PlacementData<TData>
    {
        public PlacementData(
            TData item,
            int anchorIndex,
            int count = 1,
            PlacementOrientation orientation = PlacementOrientation.Rot0)
        {
            Item = item;
            AnchorIndex = anchorIndex;
            Count = count;
            Orientation = orientation;
        }

        public TData Item { get; }
        public int AnchorIndex { get; }
        public int Count { get; }
        public PlacementOrientation Orientation { get; }
    }

    /// <summary>
    /// Context passed when a placement-aware binding commits a UI change back to external data.
    /// </summary>
    public readonly struct PlacementCommitContext<TData, TAdapter>
        where TAdapter : class, IItemAdapter
    {
        public PlacementCommitContext(
            InventoryItemEventContext eventContext,
            TData data,
            TAdapter adapter)
        {
            EventContext = eventContext;
            Data = data;
            Adapter = adapter;
        }

        public InventoryItemEventContext EventContext { get; }
        public TData Data { get; }
        public TAdapter Adapter { get; }
        public ItemStack Stack => EventContext?.Stack ?? ItemStack.Empty();
        public int Count => Stack.Count;
        public int AnchorIndex => EventContext?.AnchorIndex ?? -1;
        public PlacementOrientation Orientation => EventContext?.Orientation ?? PlacementOrientation.Rot0;
        public Footprint Footprint => EventContext?.Footprint ?? Footprint.One;
        public PlacementSnapshot PlacementSnapshot => EventContext?.PlacementSnapshot;
        public IReadOnlyList<int> CoveredIndices => EventContext?.CoveredIndices ?? Array.Empty<int>();
    }

    /// <summary>
    /// DataBinding template for inventories that persist anchor/orientation placement data.
    /// Slot inventories use the same data shape and collapse placements into one slot.
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

            foreach (var placement in placements)
            {
                if (Inventory.Grid.HasValue)
                    ReloadGridPlacement(placement);
                else
                    ReloadSlotPlacement(placement);
            }

            Inventory.UpdateAllVisuals();
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context?.Stack?.PrimaryAdapter is not TAdapter adapter)
                return;

            AddPlacementData(new PlacementCommitContext<TData, TAdapter>(
                context,
                ExtractData(adapter),
                adapter));
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context?.Stack?.PrimaryAdapter is not TAdapter adapter)
                return;

            RemovePlacementData(new PlacementCommitContext<TData, TAdapter>(
                context,
                ExtractData(adapter),
                adapter));
        }

        private void ReloadSlotPlacement(PlacementData<TData> placement)
        {
            int count = Math.Max(1, placement.Count);
            int targetSlotIndex = placement.AnchorIndex >= 0 ? placement.AnchorIndex : -1;
            if (targetSlotIndex >= 0 && TryCreateStack(placement.Item, count, out var stack))
            {
                var request = new PlacementRequest(
                    stack,
                    targetSlotIndex,
                    placement.Orientation,
                    PlacementShapeUtility.Resolve(stack.PrimaryAdapter));

                if (!Inventory.TryPlace(request))
                    OnPlacementReloadFailed(placement, stack.PrimaryAdapter);

                return;
            }

            AddToUIQuiet(() => CreateAdapter(placement.Item), count, targetSlotIndex);
        }

        private void ReloadGridPlacement(PlacementData<TData> placement)
        {
            int count = Math.Max(1, placement.Count);
            if (!TryCreateStack(placement.Item, count, out var stack))
                return;

            var request = new PlacementRequest(
                stack,
                Math.Max(0, placement.AnchorIndex),
                placement.Orientation,
                PlacementShapeUtility.Resolve(stack.PrimaryAdapter));

            if (!Inventory.TryPlace(request))
                OnPlacementReloadFailed(placement, stack.PrimaryAdapter);
        }

        private bool TryCreateStack(TData item, int count, out ItemStack stack)
        {
            stack = ItemStack.Empty();
            if (count <= 0)
                return false;

            var adapters = new List<IItemAdapter>(count);
            for (int i = 0; i < count; i++)
            {
                var adapter = CreateAdapter(item);
                if (adapter == null)
                    return false;

                adapters.Add(adapter);
            }

            return ItemStack.TryCreate(adapters, out stack);
        }

        protected virtual void OnPlacementReloadFailed(PlacementData<TData> placement, IItemAdapter adapter)
        {
            Debug.LogWarning(
                $"[{GetType().Name}] Could not place '{adapter?.DisplayName ?? "item"}' at anchor {placement.AnchorIndex}.",
                this);
        }
    }
}
