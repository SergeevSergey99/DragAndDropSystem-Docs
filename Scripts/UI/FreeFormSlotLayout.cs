using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools;

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
    /// (localPosition / containerSize) in the data model (for example, in IItemAdapter or a separate map).
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

        // Source captured at drop time, used for same-inventory relocation.
        private BaseSlot _dragSourceBaseSlot;
        private int _dragSourceCountBefore;
        private int _dragAmount;

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
            Debug.Log($"[FreeForm-DIAG] OnEnable on '{name}': inventory={(_inventory != null ? _inventory.name : "NULL")}, type={(_inventory != null ? _inventory.GetType().Name : "-")}");
            if (_inventory == null)
                return;

            _inventory.OnSlotCreated += HandleSlotCreated;
            DragAndDropManager.OnDragStarted += HandleDragStarted;
            DragAndDropManager.OnDropAttempting += HandleDropAttempting;
            DragAndDropManager.OnDropCompleted += HandleDropCompleted;
            DragAndDropManager.OnDragCancelled += HandleDragCancelled;
        }

        private void OnDisable()
        {
            if (_inventory != null)
                _inventory.OnSlotCreated -= HandleSlotCreated;

            DragAndDropManager.OnDragStarted -= HandleDragStarted;
            DragAndDropManager.OnDropAttempting -= HandleDropAttempting;
            DragAndDropManager.OnDropCompleted -= HandleDropCompleted;
            DragAndDropManager.OnDragCancelled -= HandleDragCancelled;
        }

        // ══════════════════════════════════════════════════════════
        //  Event handlers
        // ══════════════════════════════════════════════════════════

        private void HandleDragStarted(DragContext context)
        {
            // Capture the source at drag start (authoritative: fires once in StartDrag for every drag).
            // The source slot still holds its full stack here; the actual split happens later during ProcessDrop.
            _dragSourceBaseSlot = null;
            _dragSourceCountBefore = 0;
            _dragAmount = 0;

            if (context == null || context.Entries.Count == 0)
            {
                Debug.Log($"[FreeForm-DIAG] DragStarted on '{name}': SKIP — context null or no entries");
                return;
            }
            if (context.IsBatchDrag)
            {
                Debug.Log($"[FreeForm-DIAG] DragStarted on '{name}': SKIP — IsBatchDrag (entries={context.Entries.Count})");
                return;
            }

            var entry = context.Entries[0];
            var sourceSlot = entry.SourceBaseSlot;
            string srcInvHash = sourceSlot?.Inventory != null ? sourceSlot.Inventory.GetHashCode().ToString() : "null";
            Debug.Log($"[FreeForm-DIAG] DragStarted on '{name}': sourceSlot={(sourceSlot != null ? sourceSlot.Index.ToString() : "null")}, sourceStack={(sourceSlot?.Stack != null ? sourceSlot.Stack.Count.ToString() : "null")}, sourceInv(hash)={srcInvHash}, myInv(hash)={_inventory.GetHashCode()}, entryStack={entry.Stack?.Count.ToString() ?? "null"}");
            if (sourceSlot == null || sourceSlot.Stack == null || !ReferenceEquals(sourceSlot.Inventory, _inventory))
            {
                Debug.Log($"[FreeForm-DIAG] DragStarted on '{name}': not from this inventory — sourceNull={sourceSlot == null}, stackNull={sourceSlot?.Stack == null}, invMismatch={(sourceSlot != null && !ReferenceEquals(sourceSlot.Inventory, _inventory))}");
                return;
            }

            _dragSourceBaseSlot = sourceSlot;
            _dragSourceCountBefore = sourceSlot.Stack.Count;
            _dragAmount = entry.Stack?.Count ?? 0;
            Debug.Log($"[FreeForm-DIAG] DragStarted on '{name}': CAPTURED source slot {sourceSlot.Index}, countBefore={_dragSourceCountBefore}, dragAmount={_dragAmount}");
        }

        private void HandleDropAttempting(DragContext context)
        {
            // Only remember where the pointer is at drop time; the source was captured at drag start.
            _pendingDropScreenPosition = Input.mousePosition;
            _hasPendingDropPosition = true;
        }

        private void HandleDropCompleted(DragContext context)
        {
            // A same-inventory drop is handled here too: the transfer pipeline either rejects it
            // (drop cancelled) or no-ops it (item returns to its source slot), because it has no
            // concept of free-form positions. We detect that and relocate to the drop point.
            TryFreeFormRelocate();
            ResetDropState();
        }

        private void HandleDragCancelled(DragContext context)
        {
            // Same-inventory free-form drops are frequently rejected by the pipeline (no empty slot,
            // duplicate-item rule, etc.) and arrive here as a cancel — this is the common move case.
            TryFreeFormRelocate();
            ResetDropState();
        }

        private void ResetDropState()
        {
            _hasPendingDropPosition = false;
            _dragSourceBaseSlot = null;
            _dragSourceCountBefore = 0;
            _dragAmount = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  Same-inventory relocation (free-form move / partial split)
        // ══════════════════════════════════════════════════════════

        private void TryFreeFormRelocate()
        {
            var sourceSlot = _dragSourceBaseSlot;

            // Only relocate an item that started in this inventory and is still in its source slot
            // (i.e. the pipeline rejected or no-op'd the drop). If items actually left the source,
            // the pipeline already placed them elsewhere — don't touch them.
            if (sourceSlot == null || !ReferenceEquals(sourceSlot.Inventory, _inventory))
                return;
            if (_dragAmount <= 0 || _dragSourceCountBefore <= 0)
                return;

            int amountInSourceNow = sourceSlot.Stack?.Count ?? 0;
            Vector2 releaseScreenPos = Input.mousePosition;
            bool inArea = IsScreenPointInArea(releaseScreenPos);
            bool overSlot = inArea && IsScreenPointOverExistingSlot(releaseScreenPos, sourceSlot);
            Debug.Log($"[FreeForm-DIAG] Relocate on '{name}': src={sourceSlot.Index}, countBefore={_dragSourceCountBefore}, countNow={amountInSourceNow}, dragAmount={_dragAmount}, inArea={inArea}, overOtherSlot={overSlot}, pos={releaseScreenPos}");

            if (amountInSourceNow != _dragSourceCountBefore)
                return;

            // The item must have been released over this inventory's free-form area, on empty space.
            // Dropping onto another existing slot is left to the pipeline (merge / swap).
            if (!inArea || overSlot)
                return;

            // HandleSlotCreated (used for the partial-split's new slot) reads this position.
            _pendingDropScreenPosition = releaseScreenPos;
            _hasPendingDropPosition = true;

            if (_dragAmount >= amountInSourceNow)
            {
                // Full-stack move: reposition the existing slot to the drop point.
                if (!sourceSlot.IsEmpty)
                    PositionSlotAtScreenPoint(sourceSlot, releaseScreenPos);
                return;
            }

            // Partial move: split the dragged amount into a new slot (positioned via OnSlotCreated).
            if (_inventory is UniversalInventory universalInventory)
                universalInventory.TrySplitIntoNewSlot(sourceSlot, _dragAmount, out _);
        }

        private bool IsScreenPointInArea(Vector2 screenPos)
        {
            var areaRect = _boundsOverride != null ? _boundsOverride : _containerRect;
            return areaRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(areaRect, screenPos, ResolveUiCamera());
        }

        private bool IsScreenPointOverExistingSlot(Vector2 screenPos, BaseSlot ignoredBaseSlot)
        {
            if (_containerRect == null || _inventory == null)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _containerRect, screenPos, ResolveUiCamera(), out var localPoint))
                return false;

            var slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || ReferenceEquals(slot, ignoredBaseSlot))
                    continue;

                var slotRect = slot.Transform as RectTransform;
                if (slotRect == null)
                    continue;

                if (GetLocalRect(GetSlotLocalPosition(slotRect), slotRect).Contains(localPoint))
                    return true;
            }

            return false;
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

            var targetCamera = ResolveUiCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _containerRect, screenPos, targetCamera, out var localPos))
                return;

            localPos = ResolveOverlap(ClampToBounds(localPos, slotRect), slotRect);
            SetSlotLocalPosition(slotRect, localPos);
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
            float startX = bounds.xMin + slotSize.x * 0.5f;
            float startY = bounds.yMax - slotSize.y * 0.5f;

            for (int i = 0; i < slots.Count; i++)
            {
                var slotRect = slots[i].Transform as RectTransform;
                if (slotRect == null)
                    continue;

                int col = i % columns;
                int row = i / columns;
                var pos = new Vector2(startX + col * cellW, startY - row * cellH);
                SetSlotLocalPosition(slotRect, ClampToBounds(pos, slotRect));
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
            float startX = bounds.xMin + slotSize.x * 0.5f;
            float startY = bounds.yMax - slotSize.y * 0.5f;

            var pos = new Vector2(startX + col * cellW, startY - row * cellH);
            SetSlotLocalPosition(slotRect, ClampToBounds(pos, slotRect));
        }

        // ══════════════════════════════════════════════════════════
        //  Bounds clamping
        // ══════════════════════════════════════════════════════════

        private Vector2 ClampToBounds(Vector2 localPos, RectTransform slotRect)
        {
            var bounds = GetBoundsRect();
            var slotSize = slotRect.rect.size;
            var pivot = slotRect.pivot;

            float minX = bounds.xMin + slotSize.x * pivot.x;
            float maxX = bounds.xMax - slotSize.x * (1f - pivot.x);
            float minY = bounds.yMin + slotSize.y * pivot.y;
            float maxY = bounds.yMax - slotSize.y * (1f - pivot.y);

            return new Vector2(
                ClampAxis(localPos.x, minX, maxX),
                ClampAxis(localPos.y, minY, maxY));
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
            var candidateRect = GetLocalRect(candidatePos, slotRect);

            var slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var existingRect = slots[i]?.Transform as RectTransform;
                if (existingRect == null || ReferenceEquals(existingRect, slotRect))
                    continue;

                if (candidateRect.Overlaps(GetLocalRect(GetSlotLocalPosition(existingRect), existingRect)))
                    return true;
            }

            return false;
        }

        private static Rect GetLocalRect(Vector2 localPosition, RectTransform rectTransform)
        {
            var size = rectTransform.rect.size;
            var pivot = rectTransform.pivot;
            var min = localPosition - Vector2.Scale(size, pivot);
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

            SetSlotLocalPosition(slotRect, ResolveOverlap(ClampToBounds(localPosition, slotRect), slotRect));
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
            var pos = GetSlotLocalPosition(slotRect);
            return new Vector2(
                Mathf.InverseLerp(bounds.xMin, bounds.xMax, pos.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, pos.y));
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
                Mathf.Lerp(bounds.xMin, bounds.xMax, normalized.x),
                Mathf.Lerp(bounds.yMin, bounds.yMax, normalized.y));
        }

        private Rect GetBoundsRect()
        {
            if (_containerRect == null)
                return new Rect(0, 0, 100, 100);

            if (_boundsOverride == null || _boundsOverride == _containerRect)
                return _containerRect.rect;

            Extensions.GetRectBoundsInParent(
                _boundsOverride,
                _containerRect,
                Extensions.GetCanvasCamera(_boundsOverride),
                ResolveUiCamera(),
                out var minLocal,
                out var maxLocal);

            return Rect.MinMaxRect(minLocal.x, minLocal.y, maxLocal.x, maxLocal.y);
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

        private Camera ResolveUiCamera()
            => _uiCamera != null ? _uiCamera : Extensions.GetCanvasCamera(_containerRect);

        private static Vector2 GetSlotLocalPosition(RectTransform slotRect)
            => slotRect != null ? (Vector2)slotRect.localPosition : Vector2.zero;

        private static void SetSlotLocalPosition(RectTransform slotRect, Vector2 localPosition)
        {
            if (slotRect == null)
                return;

            var position = slotRect.localPosition;
            position.x = localPosition.x;
            position.y = localPosition.y;
            slotRect.localPosition = position;
        }

        private static float ClampAxis(float value, float min, float max)
            => min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
    }
}
