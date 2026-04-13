using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Universal slot with a visual layer based on Image + Text.
    /// Extend it by overriding virtual render methods
    /// and the <see cref="OnVisualsUpdated"/> hook.
    /// </summary>
    public class UniversalSlot : BaseSlot
    {
        [Header("Visual Components")]
        [SerializeField] protected Image _iconImage;
        // Replace to TMP Support
        // [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private Text _countText;
        [SerializeField] private GameObject _countContainer;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Color _emptyColor       = new Color(1, 1, 1, 0.3f);
        [SerializeField] private Color _normalColor      = Color.white;
        [SerializeField] private Color _highlightColor   = Color.yellow;

        [Header("Filter Settings")]
        [SerializeField, Tooltip("Tint color for inactive (filtered) slots")]
        private Color _nonInteractableColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [SerializeField, Tooltip("Optional: CanvasGroup for controlling interactivity")]
        private CanvasGroup _canvasGroup;

        protected override void RenderFilled()
        {
            _iconImage.sprite  = Stack.Icon;
            _iconImage.color   = ResolveIconColor();
            _iconImage.enabled = true;
            RenderCounter();
        }

        protected override void RenderEmpty()
        {
            _iconImage.sprite  = null;
            _iconImage.color   = _emptyColor;
            _iconImage.enabled = false;
            if (_countContainer != null)
                _countContainer.SetActive(false);
        }
        
        /// <summary>Updates the stack counter. It is shown only when there is more than one item.</summary>
        protected virtual void RenderCounter()
        {
            if (_countContainer == null || !_showCount) return;

            bool shouldShow = !IsEmpty && Stack.Count > 1;
            _countContainer.SetActive(shouldShow);

            if (shouldShow && _countText != null)
                _countText.text = Stack.Count.ToString();
        }
        
        public override void Highlight(bool highlight)
        {
            _isHighlighted = highlight;
            if (_iconImage != null)
                _iconImage.color = ResolveIconColor();
        }

        /// <summary>
        /// Updates visuals when interactability changes.
        /// CanvasGroup blocks raycasts; icon color is resolved through ResolveIconColor
        /// so it does not conflict with Highlight and other states.
        /// </summary>
        protected override void UpdateInteractableVisuals()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable  = IsInteractable;
                _canvasGroup.blocksRaycasts = IsInteractable;
                _canvasGroup.alpha         = IsInteractable ? 1f : 0.5f;
            }

            if (_iconImage != null)
                _iconImage.color = ResolveIconColor();
        }
        
        /// <summary>
        /// Returns the current icon color taking all active states into account.
        /// Priority: NonInteractable -> Highlighted -> Normal / Empty.
        /// </summary>
        private Color ResolveIconColor()
        {
            if (!IsInteractable) return _nonInteractableColor;
            if (_isHighlighted)  return _highlightColor;
            return IsEmpty ? _emptyColor : _normalColor;
        }
    }
}
