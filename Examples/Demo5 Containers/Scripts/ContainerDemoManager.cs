using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Stores player inventory data and creates starting items.
    /// </summary>
    public class ContainerDemoManager : MonoSingleton<ContainerDemoManager>
    {
        [SerializeReference, ManagedReferencePicker]
        [Tooltip("Starting items in the player's inventory")]
        private List<IContainerizeItemInstance> _items = new();
        
        public IReadOnlyList<IContainerizeItemInstance> Items =>  _items; 

        public void AddPlayerItem(IContainerizeItemInstance item)
        {
            if (item != null)
                _items.Add(item);
        }

        public bool RemovePlayerItem(IContainerizeItemInstance item)
            => item != null && _items.Remove(item);

    }
}
