using System;
using DragAndDropSystem.Core;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples
{
    /// <summary>
    /// Адаптер для ItemSO, чтобы работать с новой системой
    /// Позволяет использовать существующие ScriptableObjects без изменений
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
