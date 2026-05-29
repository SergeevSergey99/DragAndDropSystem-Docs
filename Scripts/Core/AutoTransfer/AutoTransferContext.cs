using System;
using System.Collections.Generic;
using UDND.Slots;
using UnityEngine.EventSystems;
using UDND.Inventories;

namespace UDND.Core
{
    [Serializable]
    public class InventoryList
    {
        public List<BaseInventory> inventories = new List<BaseInventory>();
    }
}
