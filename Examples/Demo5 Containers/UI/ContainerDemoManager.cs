using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Хранит данные инвентаря игрока и создаёт стартовые предметы.
    /// </summary>
    public class ContainerDemoManager : MonoBehaviour
    {
        [Header("Starting Items")]
        [SerializeField, Tooltip("Предметы в инвентаре игрока при старте")]
        private List<ContainerItemSO> _startingItems = new();

        [Header("Pre-filled Container")]
        [SerializeField, Tooltip("Контейнер с предметами внутри (опционально)")]
        private ContainerItemSO _prefilledContainerSO;

        [SerializeField, Tooltip("Содержимое предзаполненного контейнера")]
        private List<ContainerItemSO> _prefilledContents = new();

        private readonly List<ItemInstance> _playerItems = new();

        public IReadOnlyList<ItemInstance> PlayerItems => _playerItems;

        public void AddPlayerItem(ItemInstance item) => _playerItems.Add(item);
        public bool RemovePlayerItem(ItemInstance item) => _playerItems.Remove(item);

        private void Awake()
        {
            foreach (var so in _startingItems)
            {
                if (so == null) continue;
                _playerItems.Add(new ItemInstance { ItemSO = so });
            }

            if (_prefilledContainerSO != null && _prefilledContainerSO.IsContainer)
            {
                var container = new ItemInstance { ItemSO = _prefilledContainerSO };
                foreach (var contentSO in _prefilledContents)
                {
                    if (contentSO == null) continue;
                    container.Contents.Add(new ItemInstance { ItemSO = contentSO });
                }
                _playerItems.Add(container);
            }
        }
    }
}
