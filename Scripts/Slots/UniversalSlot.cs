using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Interaction;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Slots
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
        [SerializeField] private Color _normalColor      = Color.white;
        [SerializeField] private Color _highlightColor   = Color.yellow;

        [SerializeField, Tooltip("Optional: CanvasGroup for controlling interactivity")]
        private SlotInputAdapter _slotInputAdapter;

        protected override void RenderFilled()
        {
            _iconImage.gameObject.SetActive(true);
            _iconImage.sprite  = Stack.Icon;
            RenderCounter();
        }

        protected override void RenderEmpty()
        {
            _iconImage.gameObject.SetActive(false);
            _countContainer.SetActive(false);
        }
        protected override void RenderFilledAndDraggedFrom()
        {
            var stack = DragAndDropManager.Instance.CurrentContext.Entries.First(e => e.SourceBaseSlot == this).Stack;
            int count = Stack.Count - stack.Count;

            if (count > 0)
            {
                RenderFilled();
                
                bool shouldShow = !IsEmpty && count > 1;
                _countContainer.SetActive(shouldShow);

                if (shouldShow && _countText != null)
                    _countText.text = count.ToString();
            }
            else
            {
                RenderEmpty();
                _countContainer.SetActive(false);
            }
            
        }

        protected override void RenderFilledAndDraggedTo() => base.RenderFilledAndDraggedTo();
        
        /// <summary>Updates the stack counter. It is shown only when there is more than one item.</summary>
        void RenderCounter()
        {
            if (_countContainer == null) return;

            bool shouldShow = !IsEmpty && Stack.Count > 1;
            _countContainer.SetActive(shouldShow);

            if (shouldShow && _countText != null)
                _countText.text = Stack.Count.ToString();
        }
        
        public override void Highlight(bool highlight)
        {
            _isHighlighted = highlight;
            if (_iconImage != null)
                _iconImage.color = highlight ? _highlightColor :  _normalColor;
        }

        /// <summary>
        /// Updates visuals when interactability changes.
        /// CanvasGroup blocks raycasts; icon color is resolved through ResolveIconColor
        /// so it does not conflict with Highlight and other states.
        /// </summary>
        protected override void UpdateInteractableVisuals()
        {
            if (_slotInputAdapter != null)
            {
                _slotInputAdapter.interactable  = IsInteractable;
                _iconImage.color = new Color(_iconImage.color.r, _iconImage.color.g, _iconImage.color.b, IsInteractable ? 1f : 0.5f);
            }
        }
    }
}
