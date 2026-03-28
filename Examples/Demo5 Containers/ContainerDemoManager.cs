using System.Collections.Generic;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Хранит данные инвентаря игрока и создаёт стартовые предметы.
    /// </summary>
    public class ContainerDemoManager : MonoBehaviour
    {
        [SerializeReference, ManagedReferencePicker]
        [Tooltip("Предметы в инвентаре игрока при старте")]
        private List<IContainerizeItemInstance> _items = new();
        
        public IReadOnlyList<IContainerizeItemInstance> Items =>  _items; 

        public void AddPlayerItem(ItemInstance item) => _items.Add(item);
        public bool RemovePlayerItem(ItemInstance item) => _items.Remove(item);

    }
}
