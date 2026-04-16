using System;
using UniversalDragAndDrop.Examples.General;
using UnityEngine;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Examples.General
{
    /// <summary>
    /// Adapter for ItemSO to work with the new system
    /// Allows existing ScriptableObjects to be used without modification
    /// </summary>
    public class ItemAdapterSoAdapter : IItemAdapter
    {
        public readonly ItemExampleSO item;

        public ItemAdapterSoAdapter(ItemExampleSO item)
        {
            this.item = item;
        }

        public string ItemId => item.GetInstanceID().ToString();
        public Sprite Icon => item.Icon;
        public string DisplayName => item.ItemName;
    }
}