using UnityEngine;
using UDND.Core;

namespace UDND.Examples.Minecraft
{
    /// <summary>
    /// Adapter for MinecraftItemSO.
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