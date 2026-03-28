using System.Collections.Generic;
using DragAndDropSystem.DataBinding;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// DataBinding для инвентаря игрока.
    /// </summary>
    public class PlayerInventoryDataBinding : ListInventoryDataBinding<ItemInstance, ContainerItemAdapter>
    {
        [SerializeField] private ContainerDemoManager _manager;

        protected override IReadOnlyList<ItemInstance> GetItems() => _manager.PlayerItems;
        protected override ContainerItemAdapter CreateAdapter(ItemInstance item) => new(item);
        protected override void AddToData(ContainerItemAdapter adapter) => _manager.AddPlayerItem(adapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapter adapter) => _manager.RemovePlayerItem(adapter.Instance);
    }
}
