using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.UI
{
    [DisallowMultipleComponent]
    //[DefaultExecutionOrder(1000)]
    public sealed class PlacementOverlay : MonoBehaviour
    {
        [SerializeField] private BaseInventory _inventory;
        [SerializeField] private RectTransform _overlayRoot;
        [SerializeField] private PlacementOverlayItem _itemPrefab;
        [SerializeField] private Color _color = Color.white;

        private readonly List<PlacementOverlayItem> _activeItems = new List<PlacementOverlayItem>();
        private readonly Stack<PlacementOverlayItem> _itemPool = new Stack<PlacementOverlayItem>();
        private readonly HashSet<Placement> _renderedPlacements = new HashSet<Placement>();
        private readonly List<BaseSlot> _coveredSlotsBuffer = new List<BaseSlot>();
        private readonly Vector3[] _corners = new Vector3[4];
        private bool _refreshScheduled;
        private bool _dimensionsDirty;
        public bool HasRenderedPlacement(Placement placement) => _renderedPlacements.Contains(placement);

        private void Awake()
        {
            if (_inventory == null)
            {
                Debug.LogError($"[{nameof(PlacementOverlay)}] No inventory assigned. Attempting to find one on the same GameObject.", this);
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<BaseInventory>();

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

            ScheduleRefresh(dimensionsChanged: true);
        }

        private void Start()
        {
            ScheduleRefresh(dimensionsChanged: true);
        }

        private void LateUpdate()
        {
            if (!_refreshScheduled)
                return;

            _refreshScheduled = false;
            bool dimensionsChanged = _dimensionsDirty;
            _dimensionsDirty = false;
            Refresh(dimensionsChanged);
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

            ReleaseAllActiveItems();
            _renderedPlacements.Clear();
            _refreshScheduled = false;
            _dimensionsDirty = false;
        }

        private void OnRectTransformDimensionsChange() => ScheduleRefresh(dimensionsChanged: true);

        [Button]
        void Refresh() => Refresh(true);

        private void Refresh(bool dimensionsChanged)
        {
            if (!isActiveAndEnabled)
                return;

            // ForceUpdateCanvases is global and expensive; only call it when slot rects could
            // have moved (layout/inventory dimension change). Item-only changes don't need it.
            if (dimensionsChanged)
                Canvas.ForceUpdateCanvases();

            ReleaseAllActiveItems();
            _renderedPlacements.Clear();

            if (_inventory == null ||
                _inventory is not IPlacementInventory placementInventory)
                return;

            var root = ResolveOverlayRoot();
            if (root == null)
                return;

            var placements = placementInventory.Placements;
            foreach (var placement in placements)
            {
                if (placement == null ||
                    placement.Stack == null ||
                    placement.Stack.IsEmpty)
                {
                    continue;
                }

                var renderState = ResolveRenderState(placement);

                // Every placement renders as a single item spanning the bounding box of its
                // occupied cells. The item's sprite conveys the actual footprint (e.g. the notch
                // of an L), so non-rectangular shapes must not be split into one item per cell.
                if (!TryRenderPlacementBounds(placement, root, renderState))
                    continue;

                _renderedPlacements.Add(placement);
            }
        }

        private bool TryRenderPlacementBounds(
            Placement placement,
            RectTransform root,
            PlacementOverlayRenderState renderState)
        {
            if (!TryGetPlacementRect(placement, root, out var rect))
                return false;

            var item = GetItem(root, placement);
            
            item.RectTransform.anchoredPosition = rect.center;
            item.RectTransform.sizeDelta = GetUnrotatedSize(rect.size, placement);
            
            item.transform.SetAsLastSibling();
            item.Render(placement, renderState, _color);
            _activeItems.Add(item);
            return true;
        }

        public IReadOnlyList<BaseSlot> CollectCoveredSlots(Placement placement)
        {
            // Reused synchronously by item.Render below; no per-placement allocation needed.
            _coveredSlotsBuffer.Clear();
            for (int i = 0; i < placement.CoveredIndices.Count; i++)
                _coveredSlotsBuffer.Add(_inventory.GetSlot(placement.CoveredIndices[i]));

            return _coveredSlotsBuffer;
        }
        public float GetRotation(Placement placement)
        {
            var topology = GetPlacementTopology();
            return topology.GetVisualAngleDegrees(placement.Orientation);
        }

        /// <summary>
        /// Size the item must have <i>before</i> the visual rotation so that, once rotated, it spans
        /// the covered-cell bounding box <paramref name="rotatedBoundsSize"/>. Derived from the shape's
        /// orientation-0 footprint vs its rotated footprint (in cells), which generalizes the old
        /// hardcoded 90-degree width/height swap to any orientation the topology defines.
        /// </summary>
        private Vector2 GetUnrotatedSize(Vector2 rotatedBoundsSize, Placement placement)
        {
            var topology = GetPlacementTopology();
            var rotatedCells = PlacementShapeUtility.GetBoundingSize(placement.Shape, placement.Orientation, topology);
            var unrotatedCells = PlacementShapeUtility.GetBoundingSize(placement.Shape, 0, topology);
            if (rotatedCells.x <= 0 || rotatedCells.y <= 0)
                return rotatedBoundsSize;

            return new Vector2(
                rotatedBoundsSize.x * unrotatedCells.x / rotatedCells.x,
                rotatedBoundsSize.y * unrotatedCells.y / rotatedCells.y);
        }

        private IInventoryTopology GetPlacementTopology()
            => (_inventory as IPlacementInventory)?.Topology ?? new RectGridTopology(1, 1);

        private RectTransform ResolveOverlayRoot()
        {
            if (_overlayRoot == null)
                _overlayRoot = transform as RectTransform;
            return _overlayRoot;
        }

        private PlacementOverlayItem GetItem(RectTransform root, Placement placement)
        {
            PlacementOverlayItem item = null;
            while (_itemPool.Count > 0)
            {
                var pooled = _itemPool.Pop();
                if (pooled == null)
                    continue;

                item = pooled;
                if (item.transform.parent != root)
                    item.transform.SetParent(root, false);
                item.gameObject.SetActive(true);
                return item;
            }

            if (_itemPrefab != null)
            {
                item = Instantiate(_itemPrefab, root);
                item.Init(this);
            }
            else
            {
                var itemObject = new GameObject(
                    $"Placement Overlay Item {placement.AnchorIndex}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(PlacementOverlayItem));
                itemObject.transform.SetParent(root, false);
                item = itemObject.GetComponent<PlacementOverlayItem>();
                item.Init(this);
            }

            return item;
        }

        private void ReleaseAllActiveItems()
        {
            for (int i = 0; i < _activeItems.Count; i++)
            {
                var item = _activeItems[i];
                if (item == null)
                    continue;

                item.gameObject.SetActive(false);
                _itemPool.Push(item);
            }

            _activeItems.Clear();
        }

        private PlacementOverlayRenderState ResolveRenderState(Placement placement)
        {
            if (IsSourcePlacementBeingDragged(placement))
                return PlacementOverlayRenderState.FilledAndDraggedFrom;

            if (HasCoveredSlotState(placement, draggedTo: true))
                return PlacementOverlayRenderState.FilledAndDraggedTo;

            return PlacementOverlayRenderState.Filled;
        }

        private bool HasCoveredSlotState(Placement placement, bool draggedTo)
        {
            if (placement == null)
                return false;

            for (int i = 0; i < placement.CoveredIndices.Count; i++)
            {
                var slot = _inventory.GetSlot(placement.CoveredIndices[i]);
                if (slot == null)
                    continue;

                if (draggedTo && slot.IsDraggedToVisualState)
                    return true;

                if (!draggedTo && slot.IsDraggedFromVisualState)
                    return true;
            }

            return false;
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
                if (!TryGetSlotWorldCorners(slot))
                    return false;

                for (int c = 0; c < _corners.Length; c++)
                {
                    var localPoint = (Vector2)root.InverseTransformPoint(_corners[c]);
                    
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

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 0f && rect.height > 0f;
        }

        private bool TryGetSlotWorldCorners(BaseSlot slot)
        {
            var slotRect = ResolveSlotRect(slot);
            if (TryGetRectWorldCorners(slotRect))
                return true;

            if (slotRect == null)
                return false;

            var childRects = slotRect.GetComponentsInChildren<RectTransform>(true);
            RectTransform bestRect = null;
            float bestArea = 0f;

            for (int i = 0; i < childRects.Length; i++)
            {
                var childRect = childRects[i];
                if (ReferenceEquals(childRect, slotRect))
                    continue;

                var size = childRect.rect.size;
                float area = size.x * size.y;
                if (area <= bestArea)
                    continue;

                if (size.x <= 0f || size.y <= 0f)
                    continue;

                bestArea = area;
                bestRect = childRect;
            }

            return TryGetRectWorldCorners(bestRect);
        }

        private bool TryGetRectWorldCorners(RectTransform rect)
        {
            if (rect == null)
                return false;

            var size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return false;

            rect.GetWorldCorners(_corners);
            return true;
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

        private void ScheduleRefresh(bool dimensionsChanged)
        {
            if (dimensionsChanged)
                _dimensionsDirty = true;

            _refreshScheduled = true;
        }

        // Drag/inventory events don't move slot rects, so we don't need a global Canvas update.
        // Layout-affecting changes go through OnRectTransformDimensionsChange and set the dirty flag.
        private void HandleDragChanged(DragContext context) => ScheduleRefresh(dimensionsChanged: false);
        private void HandleDragEnded() => ScheduleRefresh(dimensionsChanged: false);
        private void HandleInventoryChanged(InventoryItemEventContext context) => ScheduleRefresh(dimensionsChanged: false);
        private void HandleContentRefreshed() => ScheduleRefresh(dimensionsChanged: true);
    }
}
