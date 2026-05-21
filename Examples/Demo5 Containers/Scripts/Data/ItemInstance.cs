using System;
using System.Collections.Generic;

namespace UDND.Examples.Containers
{
    /// <summary>
    /// Runtime item instance. For containers it stores nested contents.
    /// </summary>
    [Serializable]
    public class ItemInstance : IContainerizeItemInstance
    {
        public BaseItemSO ItemSO;

        public BaseItemSO GetItem() => ItemSO;
    }
}
