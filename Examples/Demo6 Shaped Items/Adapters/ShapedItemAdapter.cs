using UnityEngine;
using UDND.Core;

namespace UDND.Examples.ShapedItems
{
    public sealed class ShapedItemAdapter : IItemAdapter, IItemPlacementShapeProvider
    {
        public readonly ShapedItemExampleSO item;
        private readonly IPlacementShape _placementShape;

        public ShapedItemAdapter(ShapedItemExampleSO item)
        {
            this.item = item;
            _placementShape = item != null
                ? new ComplexPlacementShape(item.GetOccupiedCells())
                : RectPlacementShape.One;
        }

        public string ItemId => item != null ? item.GetInstanceID().ToString() : "missing-shaped-item";
        public Sprite Icon => item != null ? item.Icon : null;
        public string DisplayName => item != null ? item.ItemName : "Missing shaped item";
        public IPlacementShape PlacementShape => _placementShape;
    }
}
