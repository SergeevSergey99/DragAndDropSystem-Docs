using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.World3D;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples
{
    /// <summary>
    /// Адаптер для ItemExampleWith3DSO
    /// Позволяет работать с ScriptableObject в системе инвентаря + поддержка 3D
    /// </summary>
    public class ItemSOWith3DAdapter : IInventoryItem, IWorld3DAdapter
    {
        public readonly ItemExampleWith3DSO item;

        public ItemSOWith3DAdapter(ItemExampleWith3DSO item)
        {
            this.item = item;
        }

        // IInventoryItem реализация
        public string ItemId => item.GetInstanceID().ToString();
        public Sprite Icon => item.Icon;
        public string DisplayName => item.ItemName;

        // IWorld3DAdapter реализация
        public GameObject WorldPrefab => item.WorldPrefab;
    }
}
