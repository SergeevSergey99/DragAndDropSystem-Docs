using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Хранит данные инвентаря игрока и создаёт стартовые предметы.
    /// </summary>
    public class ContainerDemoManager : MonoSingleton<ContainerDemoManager>
    {
        [SerializeReference, ManagedReferencePicker]
        [Tooltip("Предметы в инвентаре игрока при старте")]
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
