using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Examples.Demo3Loot;
using DragAndDropSystem.World3D;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples
{
    /// <summary>
    /// Адаптер для ItemExampleWith3DSO
    /// Позволяет работать с ScriptableObject в системе инвентаря + поддержка 3D
    /// </summary>
    public class ItemAdapterSoWith3DAdapter : IItemAdapter, IWorld3DAdapter, IFilterable
    {
        public readonly ItemExampleWith3DSO item;

        public ItemAdapterSoWith3DAdapter(ItemExampleWith3DSO item)
        {
            this.item = item;
        }

        // IItemAdapter реализация
        public string ItemId => item.GetInstanceID().ToString();
        public Sprite Icon => item.Icon;
        public string DisplayName => item.ItemName;

        // IWorld3DAdapter реализация
        public GameObject WorldPrefab => item.WorldPrefab.gameObject;
        
        // IFilterable
        public string Category => item.itemType;
        public string Subcategory => item.ItemName;
        public int Rarity => 0;
    }
}
