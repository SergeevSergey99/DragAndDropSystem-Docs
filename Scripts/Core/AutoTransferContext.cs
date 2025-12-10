using System;
using System.Collections.Generic;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Inventories;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Core
{
    [Serializable]
    public class InventoryList
    {
        public List<UniversalInventory> inventories = new List<UniversalInventory>();
    }
}
