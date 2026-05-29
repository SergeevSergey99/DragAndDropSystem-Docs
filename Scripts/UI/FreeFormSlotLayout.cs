using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.UI
{
    /// <summary>
    /// Example layout component for inventories with freely placed slots.
    /// Slots are positioned at the drop point instead of using a grid/LayoutGroup.
    ///
    /// <b>Usage:</b>
    /// <list type="number">
    /// <item>Add it to the same GameObject as BaseInventory (or assign explicitly).</item>
    /// <item>Slot Management = Dynamic, Max Free Slots = 0.</item>
    /// <item>Do NOT put a LayoutGroup on the slot container (_slotContainer).</item>
    /// <item>An InventoryDropArea should be present nearby (standard one, unchanged).</item>
    /// </list>
    ///
    /// <b>Persistence (extension):</b>
    /// To persist slot positions, normalized coordinates can be stored
    /// (anchoredPosition / containerSize) in the data model (for example, in IItemAdapter or a separate map).
    /// On ReloadUI, call <see cref="ArrangeAllSlots"/> or restore positions manually:
    /// <code>
    /// foreach (var slot in inventory.Slots)
    /// {
    ///     var normalizedPos = myModel.GetSlotPosition(slot.Index);
    ///     var localPos = NormalizedToLocal(normalizedPos);
    ///     SetSlotPosition(slot, localPos);
    /// }
    /// </code>
    /// </summary>
    [AddComponentMenu("DragAndDrop/Examples/Free Form Slot Layout")]
    public class FreeFormSlotLayout : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Inventory. If not assigned, taken from the same GameObject.")]
        private BaseInventory _inventory;

        [SerializeField, Tooltip("UI camera. Null for Screen Space - Overlay Canvas.")]
        private Camera _uiCamera;

        [Header("Auto Layout (for ReloadUI / initialization)")]
        [SerializeField, Tooltip("Minimum spacing between slots during automatic placement.")]
        private float _slotSpacing = 8f;

        [Header("Bounds")]
        [SerializeField, Tooltip("Area restricting slot positions. Null uses the slot container RectTransform.")]
        private RectTransform _boundsOverride;

        // ══════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════

        private RectTransform _containerRect;
        private Vector2 _pendingDropScreenPosition;
        private bool _hasPendingDropPosition;

        // ══════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<BaseInventory>();
        }

        private void OnEnable()
        {
            CacheContainerRect();

            _inventory.OnSlotCreated += HandleSlotCreated;
            DragAndDropManager.OnDropAttempting += HandleDropAttempting;
            DragAndDropManager.OnDropCompleted += HandleDropEnded;
            DragAndDropManager.OnDragCancelled += HandleDropEnded;
        }

        private void OnDisable()
        {
            _inventory.OnSlotCreated -= HandleSlotCreated;
            DragAndDropManager.OnDropAttempting -= HandleDropAttempting;
            DragAndDropManager.OnDropCompleted -= HandleDropEnded;
            DragAndDropManager.OnDragCancelled -= HandleDropEnded;
        }

        // ══════════════════════════════════════════════════════════
        //  Event handlers
        // ══════════════════════════════════════════════════════════

        private void HandleDropAttempting(DragContext context)
        {
            _pendingDropScreenPosition = Input.mousePosition;
            _hasPendingDropPosition = true;
        }

        private void HandleDropEnded(DragContext context)
        {
            _hasPendingDropPosition = false;
        }

        private void HandleSlotCreated(BaseSlot baseSlot)
        {
            if (_containerRect == null)
                CacheContainerRect();

            if (_hasPendingDropPosition)
            {
                PositionSlotAtScreenPoint(baseSlot, _pendingDropScreenPosition);
            }
            else
            {
                PositionSlotInAutoLayout(baseSlot);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  Drop positioning
        // ══════════════════════════════════════════════════════════

        private void PositionSlotAtScreenPoint(BaseSlot baseSlot, Vector2 screenPos)
        {
            var slotRect = baseSlot.Transform as RectTransform;
            if (slotRect == null || _containerRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _containerRect, screenPos, _uiCamera, out var localPos))
                return;

            localPos = ResolveOverlap(ClampToBounds(localPos, slotRect), slotRect);
            slotRect.anchoredPosition = localPos;
        }

        // ══════════════════════════════════════════════════════════
        //  Auto layout (ReloadUI / initialization)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Arrange all current inventory slots in a non-overlapping grid.
        /// Call after ReloadUI or during initial load.
        /// </summary>
        public void ArrangeAllSlots()
        {
            if (_containerRect == null)
                CacheContainerRect();

            var slots = _inventory.Slots;
            if (slots == null || slots.Count == 0)
                return;

            var slotSize = GetSlotSize(slots[0]);
            var bounds = GetBoundsRect();
            float cellW = slotSize.x + _slotSpacing;
            float cellH = slotSize.y + _slotSpacing;

            int columns = Mathf.Max(1, Mathf.FloorToInt(bounds.width / cellW));

            // Offset from the top-left corner
            float startX = -bounds.width * 0.5f + slotSize.x * 0.5f;
            float startY = bounds.height * 0.5f - slotSize.y * 0.5f;

            for (int i = 0; i < slots.Count; i++)
            {
                var slotRect = slots[i].Transform as RectTransform;
                if (slotRect == null)
                    continue;

                int col = i % columns;
                int row = i / columns;
                var pos = new Vector2(startX + col * cellW, startY - row * cellH);
                slotRect.anchoredPosition = ClampToBounds(pos, slotRect);
            }
        }

        private void PositionSlotInAutoLayout(BaseSlot baseSlot)
        {
            var slotRect = baseSlot.Transform as RectTransform;
            if (slotRect == null || _containerRect == null)
                return;

            var slotSize = GetSlotSize(baseSlot);
            var bounds = GetBoundsRect();
            float cellW = slotSize.x + _slotSpacing;
            float cellH = slotSize.y + _slotSpacing;

            int columns = Mathf.Max(1, Mathf.FloorToInt(bounds.width / cellW));
            int index = baseSlot.Index;

            int col = index % columns;
            int row = index / columns;
            float startX = -bounds.width * 0.5f + slotSize.x * 0.5f;
            float startY = bounds.height * 0.5f - slotSize.y * 0.5f;

            var pos = new Vector2(startX + col * cellW, startY - row * cellH);
            slotRect.anchoredPosition = ClampToBounds(pos, slotRect);
        }

        // ══════════════════════════════════════════════════════════
        //  Bounds clamping
        // ══════════════════════════════════════════════════════════

        private Vector2 ClampToBounds(Vector2 localPos, RectTransform slotRect)
        {
            var bounds = GetBoundsRect();
            var slotSize = slotRect.rect.size;
            var pivot = slotRect.pivot;

            // Calculate the valid range so the slot remains fully inside bounds
            float halfW = bounds.width * 0.5f;
            float halfH = bounds.height * 0.5f;
            float minX = -halfW + slotSize.x * pivot.x;
            float maxX = halfW - slotSize.x * (1f - pivot.x);
            float minY = -halfH + slotSize.y * pivot.y;
            float maxY = halfH - slotSize.y * (1f - pivot.y);

            return new Vector2(
                Mathf.Clamp(localPos.x, minX, maxX),
                Mathf.Clamp(localPos.y, minY, maxY));
        }

        // ══════════════════════════════════════════════════════════
        //  Overlap avoidance
        // ══════════════════════════════════════════════════════════

        private Vector2 ResolveOverlap(Vector2 desiredPos, RectTransform slotRect)
        {
            if (_inventory == null)
                return desiredPos;

            if (!IntersectsAnySlot(desiredPos, slotRect))
                return desiredPos;

            var slotSize = slotRect.rect.size;
            float stepX = Mathf.Max(1f, slotSize.x + _slotSpacing);
            float stepY = Mathf.Max(1f, slotSize.y + _slotSpacing);

            int maxRadius = Mathf.Max(8, _inventory.Slots.Count + 2);
            Vector2 bestPosition = desiredPos;
            float bestDistance = float.PositiveInfinity;

            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool foundFreePosition = false;

                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                            continue;

                        var offset = new Vector2(x * stepX, y * stepY);
                        var candidate = ClampToBounds(desiredPos + offset, slotRect);

                        if (IntersectsAnySlot(candidate, slotRect))
                            continue;

                        float distance = (candidate - desiredPos).sqrMagnitude;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestPosition = candidate;
                            foundFreePosition = true;
                        }
                    }
                }

                if (foundFreePosition)
                    return bestPosition;
            }

            return bestPosition;
        }

        private bool IntersectsAnySlot(Vector2 candidatePos, RectTransform slotRect)
        {
            var candidateRect = GetAnchoredRect(candidatePos, slotRect);

            var slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var existingRect = slots[i]?.Transform as RectTransform;
                if (existingRect == null || ReferenceEquals(existingRect, slotRect))
                    continue;

                if (candidateRect.Overlaps(GetAnchoredRect(existingRect.anchoredPosition, existingRect)))
                    return true;
            }

            return false;
        }

        private static Rect GetAnchoredRect(Vector2 anchoredPosition, RectTransform rectTransform)
        {
            var size = rectTransform.rect.size;
            var pivot = rectTransform.pivot;
            var min = anchoredPosition - Vector2.Scale(size, pivot);
            return new Rect(min, size);
        }

        // ══════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Set slot position in the container's local coordinates.
        /// Useful for manual position restoration from persistence.
        /// </summary>
        public void SetSlotPosition(BaseSlot baseSlot, Vector2 localPosition)
        {
            var slotRect = baseSlot?.Transform as RectTransform;
            if (slotRect == null)
                return;

            slotRect.anchoredPosition = ResolveOverlap(ClampToBounds(localPosition, slotRect), slotRect);
        }

        /// <summary>
        /// Get the slot's normalized position (0..1, 0..1) relative to the container.
        /// Useful for persistence, preserving position independently of container size.
        /// </summary>
        public Vector2 GetNormalizedPosition(BaseSlot baseSlot)
        {
            var slotRect = baseSlot?.Transform as RectTransform;
            if (slotRect == null || _containerRect == null)
                return Vector2.zero;

            var bounds = GetBoundsRect();
            var pos = slotRect.anchoredPosition;
            return new Vector2(
                Mathf.InverseLerp(-bounds.width * 0.5f, bounds.width * 0.5f, pos.x),
                Mathf.InverseLerp(-bounds.height * 0.5f, bounds.height * 0.5f, pos.y));
        }

        /// <summary>
        /// Convert a normalized position (0..1) back into local coordinates.
        /// </summary>
        public Vector2 NormalizedToLocal(Vector2 normalized)
        {
            if (_containerRect == null)
                CacheContainerRect();

            var bounds = GetBoundsRect();
            return new Vector2(
                Mathf.Lerp(-bounds.width * 0.5f, bounds.width * 0.5f, normalized.x),
                Mathf.Lerp(-bounds.height * 0.5f, bounds.height * 0.5f, normalized.y));
        }

        private Rect GetBoundsRect()
        {
            var rt = _boundsOverride != null ? _boundsOverride : _containerRect;
            return rt != null ? rt.rect : new Rect(0, 0, 100, 100);
        }

        private void CacheContainerRect()
        {
            var container = _inventory != null ? _inventory.SlotContainer : null;
            _containerRect = container as RectTransform;
        }

        private static Vector2 GetSlotSize(BaseSlot baseSlot)
        {
            var rt = baseSlot?.Transform as RectTransform;
            return rt != null ? rt.rect.size : new Vector2(64, 64);
        }
    }
}
