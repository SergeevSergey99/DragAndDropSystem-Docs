using System.Collections.Generic;
using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// DataBinding для содержимого контейнера.
    /// Динамически привязывается к конкретному контейнеру через BindToContainer().
    /// </summary>
    public class ContainerInventoryDataBinding : ListInventoryDataBinding<ItemInstance, ContainerItemAdapter>
    {
        private ItemInstance _container;

        /// <summary> К какому контейнеру привязан биндинг. </summary>
        public ItemInstance BoundContainer => _container;

        /// <summary> Текущая глубина вложенности (0 = корневой контейнер). </summary>
        public int CurrentNestingDepth { get; private set; }

        /// <summary>
        /// Привязать к конкретному контейнеру.
        /// </summary>
        public void BindToContainer(ItemInstance container, int nestingDepth = 0)
        {
            _container = container;
            CurrentNestingDepth = nestingDepth;

            if (_container != null)
                ReloadUI();
            else
                ClearUI();
        }

        protected override IReadOnlyList<ItemInstance> GetItems() => _container?.Contents;
        protected override ContainerItemAdapter CreateAdapter(ItemInstance item) => new(item);

        protected override void AddToData(ContainerItemAdapter adapter)
        {
            _container?.Contents.Add(adapter.Instance);
        }

        protected override void RemoveFromData(ContainerItemAdapter adapter)
        {
            _container?.Contents.Remove(adapter.Instance);
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (_container == null)
                return RuleResult.Failure("No container bound");

            // Проверка ёмкости
            if (_container.Contents.Count >= _container.Capacity)
                return RuleResult.Failure("Container is full");

            // Проверка фильтра категорий
            var allowed = _container.ContainerType?.AllowedCategories;
            if (allowed != null && allowed.Length > 0 && entry.Stack?.Item is ContainerItemAdapter adapter)
            {
                var category = adapter.Instance.ItemSO.Category;
                if (!allowed.Contains(category))
                    return RuleResult.Failure($"This container only accepts: {string.Join(", ", allowed)}");
            }

            return base.CanDrop(context, entry);
        }
    }
}
