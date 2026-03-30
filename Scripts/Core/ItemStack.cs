using System;
using System.Collections.Generic;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Стак предметов. Хранит список адаптеров — каждый элемент является
    /// отдельным экземпляром предмета. При одинаковом ItemId экземпляры
    /// всё равно могут различаться runtime-данными (например, датой получения).
    ///
    /// Для создания стаков из N одинаковых предметов (fungible/preview)
    /// используй статический метод <see cref="Repeat"/>.
    /// Лимиты стака задаются через Max Stack Size в UniversalInventory
    /// или через IStackSizeLimitable на конкретном предмете.
    /// </summary>
    [Serializable]
    public class ItemStack
    {
        private readonly List<IItemAdapter> _adapters;

        public IReadOnlyList<IItemAdapter> Adapters => _adapters;

        /// Первый адаптер в стаке — представитель типа предмета.
        /// Используется для отображения иконки, имени, правил фильтрации.
        public IItemAdapter ItemAdapter => _adapters.Count > 0 ? _adapters[0] : null;

        public int Count => _adapters.Count;

        public bool IsEmpty => _adapters.Count == 0;

        /// Одиночный предмет.
        public ItemStack(IItemAdapter itemAdapter)
        {
            _adapters = new List<IItemAdapter>();
            if (itemAdapter != null)
                _adapters.Add(itemAdapter);
        }

        /// Стак из конкретных экземпляров. Список копируется.
        public ItemStack(IReadOnlyList<IItemAdapter> adapters)
        {
            _adapters = adapters != null ? new List<IItemAdapter>(adapters) : new List<IItemAdapter>();
        }

        // Внутренний конструктор — принимает уже готовый список без копирования.
        private ItemStack(List<IItemAdapter> adapters)
        {
            _adapters = adapters ?? new List<IItemAdapter>();
        }

        public static ItemStack Empty() => new ItemStack((IItemAdapter)null);

        /// <summary>
        /// Создаёт стак из N ссылок на один адаптер.
        /// Используется для fungible-предметов (все экземпляры идентичны)
        /// и для контекстов планирования/валидации, где важен тип, а не конкретный экземпляр.
        /// </summary>
        public static ItemStack Repeat(IItemAdapter itemAdapter, int count)
        {
            if (itemAdapter == null || count <= 0)
                return Empty();
            var list = new List<IItemAdapter>(count);
            for (int i = 0; i < count; i++)
                list.Add(itemAdapter);
            return new ItemStack(list);
        }

        /// <summary>
        /// Проверить, можно ли стакнуть с другим предметом (одинаковый ItemId).
        /// </summary>
        public bool CanStack(IItemAdapter otherItemAdapter)
        {
            if (ItemAdapter == null || otherItemAdapter == null) return false;
            return ItemAdapter.ItemId == otherItemAdapter.ItemId;
        }

        /// Добавить один адаптер в стак.
        public void AddToStack(IItemAdapter adapter)
        {
            if (adapter != null)
                _adapters.Add(adapter);
        }

        /// Добавить несколько адаптеров в стак.
        public void AddToStack(IReadOnlyList<IItemAdapter> adapters)
        {
            if (adapters == null) return;
            _adapters.AddRange(adapters);
        }

        /// <summary>
        /// Удалить <paramref name="amount"/> предметов из стака.
        /// Возвращает фактически удалённое количество.
        /// Используй <see cref="TakeAdapters"/> если нужны сами адаптеры.
        /// </summary>
        public int RemoveFromStack(int amount)
        {
            int toRemove = Math.Min(amount, _adapters.Count);
            if (toRemove > 0)
                _adapters.RemoveRange(_adapters.Count - toRemove, toRemove);
            return toRemove;
        }

        /// <summary>
        /// Извлечь <paramref name="amount"/> адаптеров из стака и вернуть их.
        /// Стак уменьшается на это количество.
        /// </summary>
        public List<IItemAdapter> TakeAdapters(int amount)
        {
            int toTake = Math.Min(amount, _adapters.Count);
            if (toTake <= 0)
                return new List<IItemAdapter>();
            int startIndex = _adapters.Count - toTake;
            var taken = _adapters.GetRange(startIndex, toTake);
            _adapters.RemoveRange(startIndex, toTake);
            return taken;
        }

        /// <summary>
        /// Разделить стак: извлечь <paramref name="amount"/> предметов в новый стак.
        /// </summary>
        public ItemStack Split(int amount)
        {
            var taken = TakeAdapters(amount);
            return taken.Count > 0 ? new ItemStack(taken) : Empty();
        }

        /// <summary>
        /// Применить конвертер к каждому адаптеру в стаке.
        /// Используется при конвертации типов предметов (например, при торговле).
        /// </summary>
        public void MapAdapters(Func<IItemAdapter, IItemAdapter> converter)
        {
            if (converter == null) return;
            for (int i = 0; i < _adapters.Count; i++)
                _adapters[i] = converter(_adapters[i]);
        }

        /// Очистить стак.
        public void Clear() => _adapters.Clear();
    }
}
