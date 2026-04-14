using DragAndDropSystem.Core;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples
{
    /// <summary>
    /// Adapter for ItemExampleWith3DSO
    /// Allows ScriptableObject-based items to work in the inventory system with 3D support
    /// </summary>
    public class ItemAdapterSoWith3DAdapter : IItemAdapter, IFilterable
    {
        public readonly ItemExampleWith3DSO item;

        public ItemAdapterSoWith3DAdapter(ItemExampleWith3DSO item)
        {
            this.item = item;
        }

        // IItemAdapter implementation
        public string ItemId => item.GetInstanceID().ToString();
        public Sprite Icon => item.Icon;
        public string DisplayName => item.ItemName;

        // IWorld3DAdapter implementation
        public GameObject WorldPrefab => item.WorldPrefab.gameObject;
        
        // IFilterable
        public string Category => item.itemType;
        public string Subcategory => item.ItemName;
        public int Rarity => 0;
    }
}