using System;
using DragAndDropSystem.Core;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples
{
    /// <summary>
    /// Адаптер для ItemModelExample, чтобы работать с новой системой
    /// Позволяет использовать существующие модели без изменений
    /// </summary>
    public class ItemModelAdapter : IInventoryItem
    {
        public readonly ItemModelExample item;

        public ItemModelAdapter(ItemModelExample item)
        {
            this.item = item;
        }

        public string ItemId => item.itemSO.GetInstanceID() + "Model";
        public Sprite Icon => item.itemSO.Icon;
        public string DisplayName => item.itemSO.ItemName;
    }
}
