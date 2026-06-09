using UnityEngine;
using UDND.Core;

namespace UDND.Examples.Craft
{
    /// <summary>
    /// Adapter for CraftItemSO.
    /// </summary>
    public class CraftItemAdapterAdapter : IItemAdapter
    {
        public readonly CraftItemSO ItemSO;

        public CraftItemAdapterAdapter(CraftItemSO item)
        {
            ItemSO = item;
        }

        public string ItemId => ItemSO.GetInstanceID().ToString();
        public Sprite Icon => ItemSO.Icon;
        public string DisplayName => ItemSO.DisplayName;
    }
}