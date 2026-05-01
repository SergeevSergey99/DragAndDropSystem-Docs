using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UniversalDragAndDrop.Slots
{
    public class ShapedColorSlot : BaseSlot
    {
        [SerializeField] protected Image _backImage;
        [SerializeField] private GameObject _countContainer;
        [SerializeField] private Text _countText;
        
        [Header("Settings")]
        [SerializeField] private Color _emptyColor      = Color.white;
        [SerializeField] private Color _filled      = Color.blue;
        [SerializeField] private Color _highlightColor   = Color.yellow;
        
        protected override void RenderFilled()
        {
            _backImage.color = _filled;
            _countContainer.SetActive(false);
        }
        protected override void RenderEmpty()
        {
            _backImage.color = _emptyColor;
            _countContainer.SetActive(false);
        }
        
        public override void Highlight(bool highlight)
        {
            _backImage.color = highlight ? _highlightColor : Stack.IsEmpty ? _emptyColor : _filled;
        }
    }
}