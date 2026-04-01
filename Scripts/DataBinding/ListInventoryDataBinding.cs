using System.Collections.Generic;
using DragAndDropSystem.Core;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// Шаблонный DataBinding для инвентарей на основе списка.
    /// Автоматически обрабатывает ReloadUI, OnItemAdded и OnItemRemoved —
    /// наследнику достаточно определить 5 методов-примитивов.
    ///
    /// TData — тип элемента данных (например, ItemSO, ItemModel)
    /// TAdapter — тип адаптера, реализующий IItemAdapter (например, ItemAdapterSoAdapter)
    ///
    /// Пример использования:
    /// <code>
    /// public class MyBinding : ListInventoryDataBinding&lt;ItemSO, ItemAdapterSoAdapter&gt;
    /// {
    ///     [SerializeField] private List&lt;ItemSO&gt; items;
    ///
    ///     protected override IReadOnlyList&lt;ItemSO&gt; GetItems() => items;
    ///     protected override ItemAdapterSoAdapter CreateAdapter(ItemSO itemAdapter) => new ItemAdapterSoAdapter(itemAdapter);
    ///     protected override ItemSO ExtractData(ItemAdapterSoAdapter adapter) => adapter.itemAdapter;
    ///     protected override void AddToData(ItemSO itemAdapter) => items.Add(itemAdapter);
    ///     protected override void RemoveFromData(ItemSO itemAdapter) => items.Remove(itemAdapter);
    /// }
    /// </code>
    /// </summary>
    public abstract class ListInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        /// <summary>
        /// Получить текущий список данных для отображения в UI.
        /// Может возвращать null — в этом случае UI останется пустым.
        /// </summary>
        protected abstract IReadOnlyList<TData> GetItems();

        /// <summary>
        /// Создать адаптер (IItemAdapter) из элемента данных.
        /// Вызывается при загрузке данных в UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Добавить элемент во внешний источник данных.
        /// Вызывается когда предмет добавлен в UI через drag&amp;drop.
        /// </summary>
        protected abstract void AddToData(TAdapter adapter);

        /// <summary>
        /// Удалить элемент из внешнего источника данных.
        /// Вызывается когда предмет удалён из UI через drag&amp;drop.
        /// </summary>
        protected abstract void RemoveFromData(TAdapter adapter);

        protected override void OnReloadUI()
        {
            var items = GetItems();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                AddToUIQuiet(CreateAdapter(item), 1);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.PrimaryAdapter is not TAdapter adapter) return;
            for (int i = 0; i < context.Count; i++)
                AddToData(context.Adapters[i] as TAdapter);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context.PrimaryAdapter is not TAdapter adapter) return;
            for (int i = 0; i < context.Count; i++)
                RemoveFromData(context.Adapters[i] as TAdapter);
        }
    }
}
