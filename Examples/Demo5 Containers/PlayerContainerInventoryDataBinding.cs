using System.Collections.Generic;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    public class PlayerContainerInventoryDataBinding : ListInventoryDataBinding<IContainerizeItemInstance, ContainerItemAdapter>
    {
        protected override IReadOnlyList<IContainerizeItemInstance> GetItems() => ContainerDemoManager.AutoCreateInstance.Items;
        protected override ContainerItemAdapter CreateAdapter(IContainerizeItemInstance item) => new(item);

        protected override void AddToData(ContainerItemAdapter adapter) => ContainerDemoManager.AutoCreateInstance.AddPlayerItem(adapter.Instance);
        protected override void RemoveFromData(ContainerItemAdapter adapter) => ContainerDemoManager.AutoCreateInstance.RemovePlayerItem(adapter.Instance);
    }
}
