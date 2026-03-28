using System;
using System.Collections.Generic;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Runtime-экземпляр предмета. Для контейнеров хранит содержимое.
    /// Обычные предметы тоже оборачиваются — единый тип для всех инвентарей.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        public ContainerItemSO ItemSO;
        public List<ItemInstance> Contents = new();

        public bool IsContainer => ItemSO != null && ItemSO.IsContainer;
        public int Capacity => ItemSO?.ContainerType?.Capacity ?? 0;
        public ContainerTypeSO ContainerType => ItemSO?.ContainerType;
    }
}
