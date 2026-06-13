using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.UI
{
    /// <summary>
    /// Drag visual that preserves the UI size of the dragged source.
    /// For shaped placements, the size is computed from all covered source slots.
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
        [SerializeField] private bool _useShapedPlacementBounds = true;
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
            if (ShouldSwapSourceSize(entry))
                size = new Vector2(size.y, size.x);

            if (size.x <= 0f || size.y <= 0f)
                size = _fallbackSize;

            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private Vector2 ResolveSourceSize(DragEntry entry)
        {
            if (_useShapedPlacementBounds &&
                entry.SourcePlacement != null &&
                !PlacementShapeUtility.IsSingleCell(
                    entry.SourcePlacement.Shape,
                    entry.SourcePlacement.Orientation,
                    entry.OrientationTopology) &&
                entry.SourceInventory != null &&
                TryGetPlacementSize(entry.SourceInventory, entry.SourcePlacement, out var placementSize))
            {
                return placementSize;
            }

            return TryGetSlotSize(entry.SourceBaseSlot, out var slotSize)
                ? slotSize
                : _fallbackSize;
        }

        private static bool ShouldSwapSourceSize(DragEntry entry)
            => IsQuarterTurn(entry.OrientationTopology, entry.Orientation);

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

        private static bool IsQuarterTurn(
            IInventoryTopology topology,
            PlacementOrientation orientation)
            => IsQuarterTurn(topology.GetVisualAngleDegrees(orientation));

        private static bool IsQuarterTurn(float angle)
            => Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, angle)) - 90f) < 0.01f;

        private bool TryGetPlacementSize(IInventory inventory, Placement placement, out Vector2 size)
        {
            size = default;
            bool hasPoint = false;
            var min = Vector2.zero;
            var max = Vector2.zero;

            for (int i = 0; i < placement.CoveredIndices.Count; i++)
            {
                var slot = inventory.GetSlot(placement.CoveredIndices[i]);
                if (!TryGetRectTransform(slot, out var slotRect))
                    return false;

                slotRect.GetWorldCorners(_corners);
                for (int c = 0; c < _corners.Length; c++)
                {
                    var localPoint = TransformPointToVisualParent(_corners[c]);
                    if (!hasPoint)
                    {
                        min = localPoint;
                        max = localPoint;
                        hasPoint = true;
                        continue;
                    }

                    min = Vector2.Min(min, localPoint);
                    max = Vector2.Max(max, localPoint);
                }
            }

            if (!hasPoint)
                return false;

            size = max - min;
            return size.x > 0f && size.y > 0f;
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
