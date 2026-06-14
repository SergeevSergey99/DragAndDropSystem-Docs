using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;
using UDND.Slots;

namespace UDND.UI
{
    /// <summary>
    /// Drag visual that preserves the UI size of the dragged source.
    /// The root is sized to the item's orientation-0 footprint (cell size x shape bounds); the
    /// current orientation is conveyed solely by rotating the icon in <see cref="ApplyOrientation"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SourceSizedDragVisual : IDragVisual
    {
        [Header("Components")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _countParent;
        [SerializeField] private Text _countText;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Vector2 _fallbackSize = new Vector2(100f, 100f);
        [SerializeField] private Color _normalColor = Color.white;

        private readonly Vector3[] _corners = new Vector3[4];

        public bool IsVisible => gameObject.activeSelf;

        public override void Show(IReadOnlyList<DragEntry> entries)
        {
            if (entries == null || entries.Count == 0 || _iconImage == null)
            {
                Hide();
                return;
            }

            var entry = entries[0];
            var stack = entry.Stack;
            if (stack == null || stack.IsEmpty)
            {
                Hide();
                return;
            }

            ApplySourceSize(entry);
            RenderStack(stack);
            ApplyOrientation(entry);
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RenderStack(ItemStack stack)
        {
            _iconImage.sprite = stack.Icon;
            _iconImage.color = _normalColor;
            _iconImage.preserveAspect = stack.Icon != null;

            if (!_showCount || _countParent == null || _countText == null)
                return;

            bool shouldShowCount = stack.Count > 1;
            _countParent.SetActive(shouldShowCount);
            if (shouldShowCount)
                _countText.text = stack.Count.ToString();
        }

        private void ApplySourceSize(DragEntry entry)
        {
            var size = ResolveSourceSize(entry);
            if (size.x <= 0f || size.y <= 0f)
                size = _fallbackSize;

            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        // Size as if the item were at orientation 0: one cell times the shape's orientation-0 bounds.
        // ApplyOrientation then rotates the icon to the current orientation.
        private Vector2 ResolveSourceSize(DragEntry entry)
        {
            if (!TryGetSlotSize(entry.SourceBaseSlot, out var cellSize))
                return _fallbackSize;

            var bounds = PlacementShapeUtility.GetBoundingSize(entry.Shape, 0, entry.OrientationTopology);
            if (bounds.x <= 0 || bounds.y <= 0)
                return cellSize;

            return new Vector2(cellSize.x * bounds.x, cellSize.y * bounds.y);
        }

        private void ApplyOrientation(DragEntry entry)
        {
            if (_iconImage != null)
            {
                _iconImage.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    entry.OrientationTopology.GetVisualAngleDegrees(entry.Orientation));
            }
        }

        private bool TryGetSlotSize(BaseSlot slot, out Vector2 size)
        {
            size = default;
            if (!TryGetRectTransform(slot, out var slotRect))
                return false;

            slotRect.GetWorldCorners(_corners);
            var min = TransformPointToVisualParent(_corners[0]);
            var max = min;
            for (int i = 1; i < _corners.Length; i++)
            {
                var localPoint = TransformPointToVisualParent(_corners[i]);
                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            size = max - min;
            return size.x > 0f && size.y > 0f;
        }

        private bool TryGetRectTransform(BaseSlot slot, out RectTransform rectTransform)
        {
            rectTransform = null;
            if (slot == null)
                return false;

            rectTransform = slot.Transform as RectTransform ?? slot.GetComponent<RectTransform>();
            return rectTransform != null && rectTransform.rect.width > 0f && rectTransform.rect.height > 0f;
        }

        private Vector2 TransformPointToVisualParent(Vector3 worldPoint)
        {
            return _rectTransform.parent is RectTransform parentRect
                ? (Vector2)parentRect.InverseTransformPoint(worldPoint)
                : (Vector2)worldPoint;
        }
    }
}
