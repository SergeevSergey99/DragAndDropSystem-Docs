using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Core;
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
        [SerializeField, Tooltip("Optional explicit graphic used for slot-level hover/drop preview. If empty, a non-raycast runtime overlay is created.")]
        private Graphic _highlightGraphic;

        [Header("Settings")]
        [SerializeField] private Color _normalColor      = Color.white;
        [SerializeField] private Color _highlightColor   = Color.yellow;

        [SerializeField, Tooltip("Optional: CanvasGroup for controlling interactivity")]
        private SlotInputAdapter _slotInputAdapter;

        private bool _hasStoredHighlightGraphicColor;
        private Color _storedHighlightGraphicColor;
        private Image _runtimeHighlightImage;

        protected override void RenderFilled()
        {
            _iconImage.gameObject.SetActive(true);
            _iconImage.sprite  = Stack.Icon;
            _iconImage.enabled = true;
            RenderCounter();
        }

        protected override void RenderEmpty()
        {
            _iconImage.enabled = true;
            _iconImage.gameObject.SetActive(false);
            _countContainer.SetActive(false);
        }
        protected override void RenderFilledAndDraggedFrom()
        {
            if (!TryGetDraggedEntryForThisSlot(out var entry))
            {
                RenderEmpty();
                return;
            }

            var stack = entry.Stack;
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

            if (_highlightGraphic != null)
            {
                if (highlight)
                {
                    if (!_hasStoredHighlightGraphicColor)
                    {
                        _storedHighlightGraphicColor = _highlightGraphic.color;
                        _hasStoredHighlightGraphicColor = true;
                    }

                    _highlightGraphic.color = _highlightColor;
                }
                else if (_hasStoredHighlightGraphicColor)
                {
                    _highlightGraphic.color = _storedHighlightGraphicColor;
                    _hasStoredHighlightGraphicColor = false;
                }
            }
            else
            {
                SetRuntimeHighlightVisible(highlight);
            }

            if (_iconImage != null && !ReferenceEquals(_iconImage, _highlightGraphic))
                _iconImage.color = highlight ? _highlightColor : _normalColor;
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

        private void SetRuntimeHighlightVisible(bool visible)
        {
            if (visible)
                EnsureRuntimeHighlightImage().color = _highlightColor;

            if (_runtimeHighlightImage != null)
                _runtimeHighlightImage.gameObject.SetActive(visible);
        }

        private Image EnsureRuntimeHighlightImage()
        {
            if (_runtimeHighlightImage != null)
                return _runtimeHighlightImage;

            var highlightObject = new GameObject("Slot Drop Preview Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            highlightObject.transform.SetParent(transform, false);
            highlightObject.transform.SetAsFirstSibling();

            var rect = highlightObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _runtimeHighlightImage = highlightObject.GetComponent<Image>();
            _runtimeHighlightImage.raycastTarget = false;

            if (_slotInputAdapter != null && _slotInputAdapter.targetGraphic is Image targetImage)
            {
                _runtimeHighlightImage.sprite = targetImage.sprite;
                _runtimeHighlightImage.type = targetImage.type;
                _runtimeHighlightImage.pixelsPerUnitMultiplier = targetImage.pixelsPerUnitMultiplier;
            }

            return _runtimeHighlightImage;
        }

        private bool TryGetDraggedEntryForThisSlot(out DragEntry entry)
        {
            entry = default;
            if (!DragAndDropManager.IsInstanceExist)
                return false;

            var context = DragAndDropManager.AutoCreateInstance.CurrentContext;
            if (context?.Entries == null)
                return false;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var candidate = context.Entries[i];
                if (candidate.SourceBaseSlot == this)
                {
                    entry = candidate;
                    return true;
                }

                if (candidate.SourcePlacement == null ||
                    !ReferenceEquals(candidate.SourceInventory, Inventory))
                    continue;

                if (candidate.SourcePlacement.CoveredIndices.Contains(Index))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
