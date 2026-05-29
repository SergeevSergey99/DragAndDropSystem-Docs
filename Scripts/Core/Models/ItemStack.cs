using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UDND.Core
{
    /// <summary>
    /// Read-only stack view exposed by live inventory slots and placements.
    /// Mutating inventory-owned stacks must go through inventory/store APIs.
    /// </summary>
    public interface IReadOnlyItemStack
    {
        IItemAdapter PrimaryAdapter { get; }
        IItemAdapter ItemAdapter { get; }
        IReadOnlyList<IItemAdapter> Adapters { get; }
        int Count { get; }
        string ID { get; }
        Sprite Icon { get; }
        string DisplayName { get; }
        Type AdapterType { get; }
        bool IsEmpty { get; }
        bool CanStack(IItemAdapter otherItemAdapter);
        ItemStack CreateCopy(int amount = -1);
    }

    /// <summary>
    /// Universal wrapper for an item with an amount
    /// Works with any type implementing IItemAdapter
    /// Stack limits are defined through the inventory's max stack size settings.
    /// or through IStackSizeLimitable on a specific item
    /// if allowItemStackOverride is enabled in the inventory
    /// </summary>
    [Serializable]
    public class ItemStack : IReadOnlyItemStack
    {
        private readonly List<IItemAdapter> _adapters = new List<IItemAdapter>();

        public IItemAdapter PrimaryAdapter { get; private set; }
        public IItemAdapter ItemAdapter => PrimaryAdapter;
        public IReadOnlyList<IItemAdapter> Adapters => _adapters;
        public int Count => _adapters.Count;

        public string ID { get; private set; }
        public Sprite Icon { get; private set; }
        public string DisplayName { get; private set; }
        public Type AdapterType { get; private set; }

        public bool IsEmpty => PrimaryAdapter == null || Count <= 0;

        private ItemStack() => RefreshHeader();

        public static ItemStack Empty() => new ItemStack();

        public static bool TryCreate(IEnumerable<IItemAdapter> adapters, out ItemStack stack)
        {
            stack = Empty();
            if (adapters == null)
                return false;

            var candidate = Empty();
            if (!candidate.TryAddToStack(adapters))
                return false;

            stack = candidate;
            return !stack.IsEmpty;
        }

        /// <summary>
        /// Check whether this stack can be stacked with another item (same ItemId)
        /// </summary>
        public bool CanStack(IItemAdapter otherItemAdapter)
        {
            if (PrimaryAdapter == null || otherItemAdapter == null)
                return false;

            return ID == otherItemAdapter.ItemId && AdapterType == otherItemAdapter.GetType();
        }

        /// <summary>
        /// Legacy API from the old "primary adapter + count" model.
        /// Multi-item stacks must now be assembled from concrete adapter instances.
        /// </summary>
        public void AddToStack(int amount)
        {
            if (amount <= 0 || PrimaryAdapter == null)
                return;

            Debug.LogError("[ItemStack] AddToStack(int) is no longer supported because it duplicates the same adapter reference. Use TryAddToStack(IEnumerable<IItemAdapter>) with unique adapter instances.");
        }

        public bool TryAddToStack(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            return TryAddToStack(stack.Adapters);
        }

        public bool TryAddToStack(IEnumerable<IItemAdapter> adapters)
        {
            if (adapters == null)
                return false;

            var list = adapters.Where(adapter => adapter != null).ToList();
            if (list.Count == 0)
                return false;

            var referenceAdapter = PrimaryAdapter;
            if (referenceAdapter == null)
                referenceAdapter = list[0];

            foreach (var adapter in list)
            {
                if (!CanAcceptAdapter(adapter, referenceAdapter))
                    return false;
            }

            _adapters.AddRange(list);
            RefreshHeader();
            return true;
        }

        /// <summary>
        /// Remove items from the end of the stack
        /// </summary>
        public int RemoveFromStack(int amount)
        {
            int toRemove = Math.Min(amount, Count);
            if (toRemove <= 0)
                return 0;

            _adapters.RemoveRange(Count - toRemove, toRemove);
            RefreshHeader();
            return toRemove;
        }

        /// <summary>
        /// Remove specific adapter instances from the stack (by reference)
        /// </summary>
        public int RemoveAdapters(IReadOnlyList<IItemAdapter> adaptersToRemove)
        {
            if (adaptersToRemove == null || adaptersToRemove.Count == 0)
                return 0;

            int removed = 0;
            foreach (var adapter in adaptersToRemove)
            {
                if (_adapters.Remove(adapter))
                    removed++;
            }

            RefreshHeader();
            return removed;
        }

        /// <summary>
        /// Split the stack into two parts
        /// </summary>
        public ItemStack Split(int amount)
        {
            int toTake = Math.Min(amount, Count);
            if (toTake <= 0)
                return Empty();

            int startIndex = Count - toTake;
            var takenAdapters = new List<IItemAdapter>(toTake);
            for (int i = startIndex; i < Count; i++)
                takenAdapters.Add(_adapters[i]);

            RemoveFromStack(toTake);

            if (TryCreate(takenAdapters, out var splitStack))
                return splitStack;

            _adapters.AddRange(takenAdapters);
            RefreshHeader();
            return Empty();
        }

        public ItemStack CreateCopy(int amount = -1)
        {
            int toCopy = amount < 0 ? Count : Math.Min(amount, Count);
            if (toCopy <= 0)
                return Empty();

            int startIndex = Count - toCopy;
            var copiedAdapters = new List<IItemAdapter>(toCopy);
            for (int i = startIndex; i < Count; i++)
                copiedAdapters.Add(_adapters[i]);

            return TryCreate(copiedAdapters, out var copiedStack) ? copiedStack : Empty();
        }

        /// <summary>
        /// Convert each adapter in the stack individually.
        /// Atomic operation: if any conversion returns null, the stack is left unchanged.
        /// </summary>
        public bool TryConvertAdapters(Func<IItemAdapter, IItemAdapter> converter)
        {
            if (converter == null || _adapters.Count == 0)
                return false;

            var results = new IItemAdapter[_adapters.Count];
            for (int i = 0; i < _adapters.Count; i++)
            {
                results[i] = converter(_adapters[i]);
                if (results[i] == null)
                    return false;
            }

            for (int i = 0; i < _adapters.Count; i++)
                _adapters[i] = results[i];

            RefreshHeader();
            return true;
        }

        /// <summary>
        /// Legacy API from the old "primary adapter + count" model.
        /// Replacing a multi-item stack with a single adapter instance is unsafe.
        /// </summary>
        public void ReplaceItem(IItemAdapter newItemAdapter)
        {
            if (newItemAdapter == null)
            {
                Clear();
                return;
            }

            if (_adapters.Count == 0)
            {
                _adapters.Add(newItemAdapter);
                RefreshHeader();
                return;
            }

            if (_adapters.Count > 1)
            {
                Debug.LogError("[ItemStack] ReplaceItem(IItemAdapter) cannot replace a multi-item stack with one shared adapter instance. Rebuild the stack from concrete adapters instead.");
                return;
            }

            _adapters[0] = newItemAdapter;
            RefreshHeader();
        }

        /// <summary>
        /// Clear the stack
        /// </summary>
        public void Clear()
        {
            _adapters.Clear();
            RefreshHeader();
        }

        private bool CanAcceptAdapter(IItemAdapter adapter, IItemAdapter referenceAdapter)
        {
            if (adapter == null) return false;
            if (referenceAdapter == null) return true;
            
            return referenceAdapter.ItemId == adapter.ItemId
                && referenceAdapter.GetType() == adapter.GetType();
        }

        private void RefreshHeader()
        {
            PrimaryAdapter = _adapters.Count > 0 ? _adapters[0] : null;
            ID = PrimaryAdapter?.ItemId;
            Icon = PrimaryAdapter?.Icon;
            DisplayName = PrimaryAdapter?.DisplayName;
            AdapterType = PrimaryAdapter?.GetType();
        }
    }
}
