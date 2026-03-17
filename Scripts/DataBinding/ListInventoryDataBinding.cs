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
    /// TAdapter — тип адаптера, реализующий IInventoryItem (например, ItemSOAdapter)
    ///
    /// Пример использования:
    /// <code>
    /// public class MyBinding : ListInventoryDataBinding&lt;ItemSO, ItemSOAdapter&gt;
    /// {
    ///     [SerializeField] private List&lt;ItemSO&gt; items;
    ///
    ///     protected override IReadOnlyList&lt;ItemSO&gt; GetItems() => items;
    ///     protected override ItemSOAdapter CreateAdapter(ItemSO item) => new ItemSOAdapter(item);
    ///     protected override ItemSO ExtractData(ItemSOAdapter adapter) => adapter.item;
    ///     protected override void AddToData(ItemSO item) => items.Add(item);
    ///     protected override void RemoveFromData(ItemSO item) => items.Remove(item);
    /// }
    /// </code>
    /// </summary>
    public abstract class ListInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IInventoryItem
    {
        /// <summary>
        /// Получить текущий список данных для отображения в UI.
        /// Может возвращать null — в этом случае UI останется пустым.
        /// </summary>
        protected abstract IReadOnlyList<TData> GetItems();

        /// <summary>
        /// Создать адаптер (IInventoryItem) из элемента данных.
        /// Вызывается при загрузке данных в UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Извлечь элемент данных из адаптера.
        /// Вызывается при добавлении/удалении предмета через drag&amp;drop.
        /// Может вернуть default если адаптер не содержит нужных данных.
        /// </summary>
        protected abstract TData ExtractData(TAdapter adapter);

        /// <summary>
        /// Добавить элемент во внешний источник данных.
        /// Вызывается когда предмет добавлен в UI через drag&amp;drop.
        /// </summary>
        protected abstract void AddToData(TData item);

        /// <summary>
        /// Удалить элемент из внешнего источника данных.
        /// Вызывается когда предмет удалён из UI через drag&amp;drop.
        /// </summary>
        protected abstract void RemoveFromData(TData item);

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
            if (context.Item is not TAdapter adapter) return;

            var data = ExtractData(adapter);
            if (data != null)
                AddToData(data);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context.Item is not TAdapter adapter) return;

            var data = ExtractData(adapter);
            if (data != null)
                RemoveFromData(data);
        }
    }
}
