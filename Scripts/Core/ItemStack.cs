using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Универсальная обертка для предмета с количеством
    /// Работает с любым типом, реализующим IItemAdapter
    /// Лимиты стака задаются через настройку Max Stack Size в UniversalInventory
    /// или через IStackSizeLimitable на конкретном предмете
    /// </summary>
    [Serializable]
    public class ItemStack
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

        public ItemStack(IItemAdapter itemAdapter, int count = 1)
        {
            int safeCount = Math.Max(0, count);
            if (itemAdapter == null || safeCount == 0)
            {
                RefreshHeader();
                return;
            }

            for (int i = 0; i < safeCount; i++)
                _adapters.Add(itemAdapter);

            RefreshHeader();
        }

        public static ItemStack Empty() => new ItemStack(null, 0);

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
        /// Проверить, можно ли стакнуть с другим предметом (одинаковый ItemId)
        /// </summary>
        public bool CanStack(IItemAdapter otherItemAdapter)
        {
            if (PrimaryAdapter == null || otherItemAdapter == null)
                return false;

            return ID == otherItemAdapter.ItemId && AdapterType == otherItemAdapter.GetType();
        }

        /// <summary>
        /// Добавить предметы к стаку без ограничений
        /// </summary>
        public void AddToStack(int amount)
        {
            if (amount <= 0 || PrimaryAdapter == null)
                return;

            for (int i = 0; i < amount; i++)
                _adapters.Add(PrimaryAdapter);
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
        /// Удалить предметы из стака
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
        /// Разделить стак на две части
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

            _adapters.RemoveRange(startIndex, toTake);
            RefreshHeader();

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
        /// Конвертировать каждый адаптер в стеке индивидуально.
        /// Атомарная операция: если хотя бы одна конвертация вернёт null, стек не изменяется.
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
        /// Заменить все адаптеры в стеке на один и тот же экземпляр.
        /// </summary>
        public void ReplaceItem(IItemAdapter newItemAdapter)
        {
            if (newItemAdapter == null)
            {
                Clear();
                return;
            }

            if (_adapters.Count == 0)
                return;

            for (int i = 0; i < _adapters.Count; i++)
                _adapters[i] = newItemAdapter;
            RefreshHeader();
        }

        /// <summary>
        /// Очистить стак
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
