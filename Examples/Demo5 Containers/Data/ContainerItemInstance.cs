using System;
using System.Collections.Generic;
using DragAndDropSystem.Inspector;
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
    }
}