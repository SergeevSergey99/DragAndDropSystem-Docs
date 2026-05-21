using UnityEngine;
using UDND.Core;

namespace UDND.Examples.ShapedItems
{
    [CreateAssetMenu(fileName = "ShapedItemExampleSO", menuName = "DragAndDrop/Examples/Shaped Item", order = 6)]
    public class ShapedItemExampleSO : ScriptableObject
    {
        [SerializeField] private string _itemName;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(1)] private int _width = 1;
        [SerializeField, Min(1)] private int _height = 1;

        public string ItemName => string.IsNullOrEmpty(_itemName) ? name : _itemName;
        public Sprite Icon => _icon;
        public Footprint Footprint => new Footprint(_width, _height);

        private void OnValidate()
        {
            _width = Mathf.Max(1, _width);
            _height = Mathf.Max(1, _height);
        }
    }
}
