using UnityEngine;
using UDND.Core;

namespace UDND.Examples.ShapedItems
{
    public sealed class ShapedItemAdapter : IItemAdapter, IItemFootprintProvider
    {
        public readonly ShapedItemExampleSO item;

        public ShapedItemAdapter(ShapedItemExampleSO item)
        {
            this.item = item;
        }

        public string ItemId => item.GetInstanceID().ToString();
        public Sprite Icon => item != null ? item.Icon : null;
        public string DisplayName => item != null ? item.ItemName : "Missing shaped item";
        public Footprint Footprint => item != null ? item.Footprint : Footprint.One;
    }
}
