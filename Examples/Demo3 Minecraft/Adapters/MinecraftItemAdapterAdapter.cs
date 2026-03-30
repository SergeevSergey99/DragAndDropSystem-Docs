using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// Адаптер для MinecraftItemSO.
    /// Реализует IStackSizeLimitable — каждый тип предмета задаёт свой лимит стака
    /// (64 для блоков/материалов, 16 для жемчуга/снежков, 1 для инструментов/брони).
    /// </summary>
    public class MinecraftItemAdapterAdapter : IItemAdapter
    {
        public readonly MinecraftItemSO ItemSO;

        public MinecraftItemAdapterAdapter(MinecraftItemSO item)
        {
            ItemSO = item;
        }

        public string ItemId => ItemSO.GetInstanceID().ToString();
        public Sprite Icon => ItemSO.Icon;
        public string DisplayName => ItemSO.DisplayName;
    }
}
