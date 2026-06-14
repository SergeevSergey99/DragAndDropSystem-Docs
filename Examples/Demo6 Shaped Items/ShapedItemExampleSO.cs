using System.Collections.Generic;
using UnityEngine;

namespace UDND.Examples.ShapedItems
{
    /// <summary>
    /// Base shaped item example. This is pure authoring data and intentionally has no dependency
    /// on the inventory/placement system types — the Demo6 adapter converts <see cref="GetOccupiedCells"/>
    /// into a placement shape. On its own it describes a plain rectangle; derived types
    /// (see <see cref="ComplexShapedItemExampleSO"/>) override the footprint. The base type exists so a
    /// single inventory binding can hold both rectangular and complex items in one seed list.
    /// </summary>
    [CreateAssetMenu(fileName = "ShapedItemExampleSO", menuName = "DragAndDrop/Examples/Shaped Item", order = 6)]
    public class ShapedItemExampleSO : ScriptableObject
    {
        [SerializeField] private string _itemName;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(1)] private int _width = 1;
        [SerializeField, Min(1)] private int _height = 1;

        public string ItemName => string.IsNullOrEmpty(_itemName) ? name : _itemName;
        public Sprite Icon => _icon;
        public Vector2Int ShapeSize => new Vector2Int(Width, Height);

        protected int Width => _width;
        protected int Height => _height;

        /// <summary>
        /// Occupied cell offsets describing this item's footprint within its bounding box.
        /// The base implementation is the full <c>Width x Height</c> rectangle.
        /// </summary>
        public virtual IReadOnlyList<Vector2Int> GetOccupiedCells()
        {
            var cells = new List<Vector2Int>(_width * _height);
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                    cells.Add(new Vector2Int(x, y));
            }

            return cells;
        }

        protected virtual void OnValidate()
        {
            _width = Mathf.Max(1, _width);
            _height = Mathf.Max(1, _height);
        }
    }
}
