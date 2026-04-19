using System;
using System.Collections.Generic;
using UniversalDragAndDrop.Slots;
using UnityEngine.EventSystems;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public class InventoryList
    {
        public List<UniversalInventory> inventories = new List<UniversalInventory>();
    }
}
