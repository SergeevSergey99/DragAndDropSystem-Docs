using System;
using System.Collections.Generic;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    [Serializable]
    public class ContainerItemInstance : IContainerizeItemInstance
    {
        [field:  SerializeField]
        public ContainerItemSO Item { get; private set; }

        [SerializeReference, ManagedReferencePicker]
        private List<IContainerizeItemInstance> _items = new();
        
        public IReadOnlyList<IContainerizeItemInstance> Items => _items;
        public BaseItemSO GetItem() => Item;

        public void AddItem(IContainerizeItemInstance instance) => _items.Add(instance);
        public void RemoveItem(IContainerizeItemInstance instance) => _items.Remove(instance);
    }
}
