using System.Collections.Generic;
using DragAndDropSystem.Core;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// Шаблонный DataBinding для инвентарей с фиксированными слотами.
    /// Автоматически обрабатывает ReloadUI, OnItemAdded и OnItemRemoved —
    /// наследнику достаточно определить 5 методов-примитивов.
    ///
    /// TData — тип элемента данных (например, ItemSO, ItemModel)
    /// TAdapter — тип адаптера, реализующий IItemAdapter (например, ItemAdapterSoAdapter)
    ///
    /// Пример использования:
    /// <code>
    /// public class MyBinding : SlotIndexedInventoryDataBinding&lt;ItemSO, ItemAdapterSoAdapter&gt;
    /// {
    ///     [SerializeField] private PlayerInventoryData _playerData;
    ///
    ///     protected override IEnumerable&lt;(int index, ItemSO itemAdapter, int count)&gt; GetOccupiedSlots()
    ///     {
    ///         var slots = _playerData.Slots;
    ///         for (int i = 0; i &lt; slots.Count; i++)
    ///             if (slots[i] != null) yield return (i, slots[i], 1);
    ///     }
    ///     protected override ItemAdapterSoAdapter CreateAdapter(ItemSO itemAdapter) => new ItemAdapterSoAdapter(itemAdapter);
    ///     protected override ItemSO ExtractData(ItemAdapterSoAdapter adapter) => adapter.itemAdapter;
    ///     protected override void AddToSlotData(int index, ItemSO itemAdapter, int count) => _playerData.SetItem(index, itemAdapter);
    ///     protected override void RemoveFromSlotData(int index, ItemSO itemAdapter, int count) => _playerData.ClearSlot(index);
    /// }
    /// </code>
    /// </summary>
    public abstract class SlotIndexedInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        /// <summary>
        /// Получить занятые слоты с их индексами, данными и количеством.
        /// Пустые слоты можно не возвращать.
        /// </summary>
        protected abstract IEnumerable<(int index, TData item, int count)> GetOccupiedSlots();

        /// <summary>
        /// Создать адаптер (IItemAdapter) из элемента данных.
        /// Вызывается при загрузке данных в UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Обновить данные слота при добавлении предмета.
        /// Вызывается когда предмет добавлен в слот через drag&amp;drop.
        /// </summary>
        protected abstract void AddToSlotData(int index, TAdapter adapter, int count);

        /// <summary>
        /// Обновить данные слота при удалении предмета.
        /// Вызывается когда предмет удалён из слота через drag&amp;drop.
        /// </summary>
        protected abstract void RemoveFromSlotData(int index, TAdapter item, int count);

        protected override void OnReloadUI()
        {
            foreach (var (index, item, count) in GetOccupiedSlots())
            {
                if (item == null) continue;

                AddToUIQuiet(() => CreateAdapter(item), count, index);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter adapter) return;

            int index = context.TargetSlot?.Index ?? -1;
            if (index < 0) return;

            AddToSlotData(index, adapter, context.Stack.Count);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter adapter) return;

            int index = context.SourceSlot?.Index ?? -1;
            if (index < 0) return;

            RemoveFromSlotData(index, adapter, context.Stack.Count);
        }
    }
}
