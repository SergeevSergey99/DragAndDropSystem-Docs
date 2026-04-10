using DragAndDropSystem.Core;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// Component for items dropped into the 3D world
    /// Stores a reference to the original IItemAdapter
    /// Can optionally be picked up back into the inventory
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        public ItemExampleWith3DSO Item { get; private set; }

        /// <summary>
        /// Initialize the item with data
        /// </summary>
        public void Initialize(ItemExampleWith3DSO item)
        {
            Item = item;
        }
    }
}
