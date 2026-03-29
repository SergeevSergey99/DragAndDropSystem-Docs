using System;
using System.Collections.Generic;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Runtime-экземпляр предмета. Для контейнеров хранит содержимое.
    /// </summary>
    [Serializable]
    public class ItemInstance : IContainerizeItemInstance
    {
        public BaseItemSO ItemSO;

        public BaseItemSO GetItem() => ItemSO;
    }
}
