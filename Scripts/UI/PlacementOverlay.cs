using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UniversalInventory))]
    public sealed class PlacementOverlay : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField] private RectTransform _overlayRoot;
        [SerializeField] private Image _imagePrefab;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private bool _hideSlotIconsForShapedItems = true;

        private readonly List<Image> _activeImages = new List<Image>();
        private readonly HashSet<Placement> _renderedPlacements = new HashSet<Placement>();
        private readonly Vector3[] _corners = new Vector3[4];

        public bool HideSlotIconsForShapedItems => _hideSlotIconsForShapedItems;
        public bool HasRenderedPlacement(Placement placement) => _renderedPlacements.Contains(placement);

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            if (_inventory != null)
            {
                _inventory.OnItemAdded += HandleInventoryChanged;
                _inventory.OnItemRemoved += HandleInventoryChanged;
                _inventory.OnContentRefreshed += HandleContentRefreshed;
            }

            DragAndDropManager.OnDragStarted += HandleDragChanged;
            DragAndDropManager.OnDragEnded += HandleDragEnded;
            DragAndDropManager.OnDragCancelled += HandleDragChanged;
            DragAndDropManager.OnDropCompleted += HandleDragChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= HandleInventoryChanged;
                _inventory.OnItemRemoved -= HandleInventoryChanged;
                _inventory.OnContentRefreshed -= HandleContentRefreshed;
            }

            DragAndDropManager.OnDragStarted -= HandleDragChanged;
            DragAndDropManager.OnDragEnded -= HandleDragEnded;
            DragAndDropManager.OnDragCancelled -= HandleDragChanged;
            DragAndDropManager.OnDropCompleted -= HandleDragChanged;
            Clear();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
                Refresh();
        }

        public void Refresh()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            Clear();

            if (_inventory == null || !_inventory.Grid.HasValue)
                return;

            var root = ResolveOverlayRoot();
            if (root == null)
                return;

            var placements = _inventory.Placements;
            foreach (var placement in placements)
            {
                if (placement == null ||
                    placement.Footprint.IsSingleCell ||
                    placement.Stack == null ||
                    placement.Stack.IsEmpty ||
                    IsSourcePlacementBeingDragged(placement))
                    continue;

                if (!TryGetPlacementRect(placement, root, out var rect))
                    continue;

                var image = CreateImage(root, placement);
                var imageRect = image.rectTransform;
                imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.anchoredPosition = rect.center;
                imageRect.sizeDelta = rect.size;
                imageRect.localEulerAngles = placement.Orientation == PlacementOrientation.Rot90
                    ? new Vector3(0f, 0f, -90f)
                    : Vector3.zero;
                image.transform.SetAsLastSibling();
                _activeImages.Add(image);
                _renderedPlacements.Add(placement);
            }
        }

        private RectTransform ResolveOverlayRoot()
        {
            if (_overlayRoot != null)
            {
                _overlayRoot.SetAsLastSibling();
                return _overlayRoot;
            }

            if (_inventory == null || _inventory.transform is not RectTransform inventoryRect)
                return null;

            var overlayObject = new GameObject("Placement Overlay", typeof(RectTransform));
            overlayObject.transform.SetParent(inventoryRect, false);

            _overlayRoot = overlayObject.GetComponent<RectTransform>();
            _overlayRoot.anchorMin = Vector2.zero;
            _overlayRoot.anchorMax = Vector2.one;
            _overlayRoot.offsetMin = Vector2.zero;
            _overlayRoot.offsetMax = Vector2.zero;
            _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            _overlayRoot.SetAsLastSibling();
            return _overlayRoot;
        }

        private Image CreateImage(RectTransform root, Placement placement)
        {
            Image image;
            if (_imagePrefab != null)
            {
                image = Instantiate(_imagePrefab, root);
            }
            else
            {
                var imageObject = new GameObject(
                    $"Placement Overlay Item {placement.AnchorIndex}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                imageObject.transform.SetParent(root, false);
                image = imageObject.GetComponent<Image>();
            }

            image.raycastTarget = false;
            image.sprite = placement.Stack.Icon;
            image.color = _color;
            image.preserveAspect = placement.Stack.Icon != null;
            image.enabled = true;
            return image;
        }

        private bool TryGetPlacementRect(Placement placement, RectTransform root, out Rect rect)
        {
            rect = default;
            bool hasPoint = false;
            var min = Vector2.zero;
            var max = Vector2.zero;

            for (int i = 0; i < placement.CoveredIndices.Count; i++)
            {
                var slot = _inventory.GetSlot(placement.CoveredIndices[i]);
                var slotRect = ResolveSlotRect(slot);
                if (slotRect == null)
                    continue;

                slotRect.GetWorldCorners(_corners);
                for (int c = 0; c < _corners.Length; c++)
                {
                    var local = (Vector2)root.InverseTransformPoint(_corners[c]);
                    if (!hasPoint)
                    {
                        min = local;
                        max = local;
                        hasPoint = true;
                        continue;
                    }

                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }

            if (!hasPoint)
                return false;

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 0f && rect.height > 0f;
        }

        private static RectTransform ResolveSlotRect(BaseSlot slot)
        {
            if (slot == null)
                return null;

            if (slot.Transform is RectTransform slotRect)
                return slotRect;

            return slot.GetComponent<RectTransform>();
        }

        private bool IsSourcePlacementBeingDragged(Placement placement)
        {
            if (!DragAndDropManager.IsInstanceExist)
                return false;

            var context = DragAndDropManager.AutoCreateInstance.CurrentContext;
            if (context?.Entries == null)
                return false;

            for (int i = 0; i < context.Entries.Count; i++)
            {
                var entry = context.Entries[i];
                if (ReferenceEquals(entry.SourceInventory, _inventory) &&
                    entry.SourcePlacement != null &&
                    ReferenceEquals(entry.SourcePlacement, placement))
                    return true;
            }

            return false;
        }

        private void Clear()
        {
            for (int i = 0; i < _activeImages.Count; i++)
            {
                if (_activeImages[i] != null)
                    Destroy(_activeImages[i].gameObject);
            }

            _activeImages.Clear();
            _renderedPlacements.Clear();
        }

        private void HandleDragChanged(DragContext context) => Refresh();
        private void HandleDragEnded() => Refresh();
        private void HandleInventoryChanged(InventoryItemEventContext context) => Refresh();
        private void HandleContentRefreshed() => Refresh();
    }
}
