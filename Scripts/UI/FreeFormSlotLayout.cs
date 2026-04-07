using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Пример layout-компонента для инвентарей со свободным размещением слотов.
    /// Слоты позиционируются в точке дропа, а не по сетке/LayoutGroup.
    ///
    /// <b>Использование:</b>
    /// <list type="number">
    /// <item>Повесить на тот же GameObject, что и UniversalInventory (или указать явно).</item>
    /// <item>Slot Management = Dynamic, Max Free Slots = 0.</item>
    /// <item>НЕ ставить LayoutGroup на контейнер слотов (_slotContainer).</item>
    /// <item>Рядом должен быть InventoryDropArea (стандартный, без изменений).</item>
    /// </list>
    ///
    /// <b>Persistence (расширение):</b>
    /// Для сохранения позиций слотов можно хранить нормализованные координаты
    /// (anchoredPosition / containerSize) в модели данных (например, в IItemAdapter или отдельной карте).
    /// При ReloadUI вызвать <see cref="ArrangeAllSlots"/> или восстановить позиции вручную:
    /// <code>
    /// foreach (var slot in inventory.Slots)
    /// {
    ///     var normalizedPos = myModel.GetSlotPosition(slot.Index);
    ///     var localPos = NormalizedToLocal(normalizedPos);
    ///     SetSlotPosition(slot, localPos);
    /// }
    /// </code>
    /// </summary>
    [RequireComponent(typeof(UniversalInventory))]
    public class FreeFormSlotLayout : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Инвентарь. Если не задан — берётся с этого же GameObject.")]
        private UniversalInventory _inventory;

        [SerializeField, Tooltip("UI-камера. Null для Screen Space - Overlay Canvas.")]
        private Camera _uiCamera;

        [Header("Auto Layout (для ReloadUI / инициализации)")]
        [SerializeField, Tooltip("Минимальный отступ между слотами при автоматическом размещении.")]
        private float _slotSpacing = 8f;

        [Header("Bounds")]
        [SerializeField, Tooltip("Область, ограничивающая позиции слотов. Null — используется RectTransform контейнера слотов.")]
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
                _inventory = GetComponent<UniversalInventory>();
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

        private void HandleSlotCreated(ISlot slot)
        {
            if (_containerRect == null)
                CacheContainerRect();

            if (_hasPendingDropPosition)
            {
                PositionSlotAtScreenPoint(slot, _pendingDropScreenPosition);
            }
            else
            {
                PositionSlotInAutoLayout(slot);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  Drop positioning
        // ══════════════════════════════════════════════════════════

        private void PositionSlotAtScreenPoint(ISlot slot, Vector2 screenPos)
        {
            var slotRect = slot.Transform as RectTransform;
            if (slotRect == null || _containerRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _containerRect, screenPos, _uiCamera, out var localPos))
                return;

            localPos = ClampToBounds(localPos, slotRect);
            slotRect.anchoredPosition = localPos;
        }

        // ══════════════════════════════════════════════════════════
        //  Auto layout (ReloadUI / инициализация)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Расположить все текущие слоты инвентаря в сетке без перекрытий.
        /// Вызывать после ReloadUI или при начальной загрузке.
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

            // Отступ от верхнего левого угла
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

        private void PositionSlotInAutoLayout(ISlot slot)
        {
            var slotRect = slot.Transform as RectTransform;
            if (slotRect == null || _containerRect == null)
                return;

            var slotSize = GetSlotSize(slot);
            var bounds = GetBoundsRect();
            float cellW = slotSize.x + _slotSpacing;
            float cellH = slotSize.y + _slotSpacing;

            int columns = Mathf.Max(1, Mathf.FloorToInt(bounds.width / cellW));
            int index = slot.Index;

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

            // Рассчитываем допустимый диапазон так, чтобы слот целиком оставался внутри bounds
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
        //  Overlap avoidance (extension point)
        // ══════════════════════════════════════════════════════════

        // TODO: Для реализации anti-overlap можно после позиционирования слота
        // проверить пересечение с существующими слотами (Rect.Overlaps) и сдвинуть
        // к ближайшей свободной позиции. Пример:
        //
        // private Vector2 ResolveOverlap(Vector2 desiredPos, RectTransform slotRect)
        // {
        //     var candidateRect = new Rect(desiredPos - slotRect.rect.size * 0.5f, slotRect.rect.size);
        //     foreach (var existing in _inventory.Slots)
        //     {
        //         if (existing.Transform == slotRect.transform) continue;
        //         var existingRT = existing.Transform as RectTransform;
        //         var existingRect = new Rect(
        //             existingRT.anchoredPosition - existingRT.rect.size * 0.5f,
        //             existingRT.rect.size);
        //         if (candidateRect.Overlaps(existingRect))
        //         {
        //             // Сдвинуть candidateRect в сторону от existingRect
        //         }
        //     }
        //     return desiredPos;
        // }

        // ══════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Установить позицию слота в локальных координатах контейнера.
        /// Удобно для ручного восстановления позиций из persistence.
        /// </summary>
        public void SetSlotPosition(ISlot slot, Vector2 localPosition)
        {
            var slotRect = slot?.Transform as RectTransform;
            if (slotRect == null)
                return;

            slotRect.anchoredPosition = ClampToBounds(localPosition, slotRect);
        }

        /// <summary>
        /// Получить нормализованную позицию слота (0..1, 0..1) относительно контейнера.
        /// Полезно для persistence — сохранение позиции независимо от размера контейнера.
        /// </summary>
        public Vector2 GetNormalizedPosition(ISlot slot)
        {
            var slotRect = slot?.Transform as RectTransform;
            if (slotRect == null || _containerRect == null)
                return Vector2.zero;

            var bounds = GetBoundsRect();
            var pos = slotRect.anchoredPosition;
            return new Vector2(
                Mathf.InverseLerp(-bounds.width * 0.5f, bounds.width * 0.5f, pos.x),
                Mathf.InverseLerp(-bounds.height * 0.5f, bounds.height * 0.5f, pos.y));
        }

        /// <summary>
        /// Конвертировать нормализованную позицию (0..1) обратно в локальные координаты.
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

        private static Vector2 GetSlotSize(ISlot slot)
        {
            var rt = slot?.Transform as RectTransform;
            return rt != null ? rt.rect.size : new Vector2(64, 64);
        }
    }
}
