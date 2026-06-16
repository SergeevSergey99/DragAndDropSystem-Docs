using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;
using UDND.Interaction;
using UDND.Slots;
using UDND.Tools;
using UDND.Tools.Inspector;

namespace UDND.UI
{
    /// <summary>
    /// Tooltip manager used to display item information when hovering slots.
    /// OPTIONAL component: works only if it is present in the scene.
    /// Subscribes to static hover events from SlotInputAdapter.
    ///
    /// Uses ITooltipView for rendering, so different tooltip prefabs can be assigned for different items.
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        [SerializeField, Required]
        private Canvas _canvas;
        [Header("Default Tooltip View")]
        [SerializeField, Required, Tooltip("Default tooltip prefab (must implement ITooltipView)")]
        private BaseTooltipView _defaultTooltipPrefab;

        [Header("Positioning")]
        [SerializeField, Tooltip("Tooltip offset from the cursor")]
        private Vector2 _offset = new Vector2(15, -15);

        [SerializeField, Tooltip("Tooltip positioning type")]
        private TooltipAnchor _anchor = TooltipAnchor.Cursor;

        [SerializeField, Tooltip("Tooltip pivot (0,0 = bottom-left corner, 1,1 = top-right corner)"), ShowIf(nameof(_anchor), TooltipAnchor.SlotPivot)]
        private Vector2 pivot;
        
        [SerializeField, Tooltip("Padding from the screen edges")]
        private float _screenPadding = 10f;

        [SerializeField, Tooltip("Minimum distance between the cursor and the card")]
        private float _cursorMargin = 5f;

        [SerializeField, Tooltip("Minimum distance between the slot and the card")]
        private float _slotMargin = 8f;

        [Header("Timing")]
        [SerializeField, Tooltip("Delay before showing the tooltip (seconds)")]
        private float _showDelay = 0.5f;
        
        private Coroutine _showCoroutine;
        private SlotHoverEventArgs _currentHoverArgs;
        private BaseTooltipView _currentBaseTooltipView;
        private bool _tooltipLayoutDirty;


        private void OnEnable()
        {
            // Subscribe to global static slot events
            UDNDEvents.OnAnySlotHoverEnter += OnSlotHoverEnter;
            UDNDEvents.OnAnySlotHoverExit += OnSlotHoverExit;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            UDNDEvents.OnAnySlotHoverEnter -= OnSlotHoverEnter;
            UDNDEvents.OnAnySlotHoverExit -= OnSlotHoverExit;

            // Stop coroutines
            StopAllTooltipCoroutines();

            // Hide the tooltip
            HideTooltip();
        }

        private void Update()
        {
            // If the tooltip is visible and anchored to the cursor, update its position
            if (_currentBaseTooltipView != null)
            {
                if (_anchor == TooltipAnchor.Cursor)
                {
                    _currentHoverArgs.ScreenPosition = Input.mousePosition;
                    UpdateTooltipPosition(_currentHoverArgs);
                }
            }
        }

        #region Event Handlers

        /// <summary>
        /// Slot hover enter handler
        /// </summary>
        private void OnSlotHoverEnter(SlotHoverEventArgs args)
        {
            // Ignore if there is no item
            if (!args.HasItem) return;

            _currentHoverArgs = args;

            // Cancel the previous show operation if there was one
            StopAllTooltipCoroutines();

            // Show with delay
            _showCoroutine = StartCoroutine(ShowTooltipDelayed(args));
        }

        /// <summary>
        /// Slot hover exit handler
        /// </summary>
        private void OnSlotHoverExit(SlotHoverEventArgs args)
        {
            // Cancel showing if it has not been shown yet
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }

            // Hide the tooltip
            HideTooltip();
        }

        #endregion

        #region Tooltip Display

        /// <summary>
        /// Show the tooltip with a delay
        /// </summary>
        private IEnumerator ShowTooltipDelayed(SlotHoverEventArgs args)
        {
            yield return new WaitForSeconds(_showDelay);
            ShowTooltip(args);
        }

        /// <summary>
        /// Show the tooltip
        /// </summary>
        private void ShowTooltip(SlotHoverEventArgs args)
        {
            if (args.ItemAdapter == null) return;

            // Hide the previous tooltip if one exists
            if (_currentBaseTooltipView != null)
            {
                HideTooltip();
            }

            // Create the tooltip view
            _currentBaseTooltipView = Instantiate(_defaultTooltipPrefab, _canvas.transform);

            if (_currentBaseTooltipView == null)
            {
                Debug.LogError("[TooltipManager] Failed to get tooltip view!");
                return;
            }

            // Show the tooltip with content
            _currentBaseTooltipView.Show(args.ItemAdapter);
            _tooltipLayoutDirty = true;

            // Position it
            UpdateTooltipPosition(args);
        }

        /// <summary>
        /// Hide the tooltip
        /// </summary>
        public void HideTooltip()
        {
            if (_currentBaseTooltipView == null) return;

            var toHide = _currentBaseTooltipView;
            _currentBaseTooltipView.Hide(() =>
            {
                Destroy(toHide.gameObject);
            });
            _currentBaseTooltipView = null;
            _tooltipLayoutDirty = false;
        }

        #endregion

        #region Positioning

        /// <summary>
        /// Update tooltip position
        /// </summary>
        private void UpdateTooltipPosition(SlotHoverEventArgs args)
        {
            if (_currentBaseTooltipView == null || args == null)
                return;

            Vector2 position;
            RefreshTooltipLayout();

            var tooltipSize = GetTooltipScreenSize();
            var tooltipPivot = _currentBaseTooltipView.rectTransform.pivot;
            var hasSlotBounds = TryGetSlotScreenBounds(args.SlotRectTransform, out var slotBounds);
            var anchorPosition = args.ScreenPosition;

            if (_anchor == TooltipAnchor.SlotPivot && hasSlotBounds)
                anchorPosition = GetPointInRect(slotBounds, pivot);

            // Use adaptive positioning
            position = CalculateAdaptivePosition(
                anchorPosition,
                tooltipSize,
                tooltipPivot,
                _offset,
                hasSlotBounds ? slotBounds : (Rect?)null
            );

            _currentBaseTooltipView.UpdatePosition(position);
        }

        /// <summary>
        /// Get the card bounds in screen coordinates
        /// </summary>
        /// <param name="position">Card position (anchor point)</param>
        /// <param name="size">Card size</param>
        /// <param name="pivot">Card pivot (0,0 = bottom-left corner, 1,1 = top-right corner)</param>
        /// <returns>Rect in screen coordinates</returns>
        private Rect GetTooltipScreenBounds(Vector2 position, Vector2 size, Vector2 pivot)
        {
            float left = position.x - size.x * pivot.x;
            float bottom = position.y - size.y * pivot.y;
            return new Rect(left, bottom, size.x, size.y);
        }

        /// <summary>
        /// Check whether a point is inside a rect
        /// </summary>
        private bool IsPointInRect(Vector2 point, Rect rect)
        {
            return rect.Contains(point);
        }

        private static bool IsRectInsideScreen(Rect rect, float padding)
        {
            return rect.xMin >= padding &&
                   rect.yMin >= padding &&
                   rect.xMax <= Screen.width - padding &&
                   rect.yMax <= Screen.height - padding;
        }

        private static float GetOverlapArea(Rect a, Rect b)
        {
            float width = Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
            float height = Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
            return width * height;
        }

        private float GetOffscreenPenalty(Rect rect)
        {
            float penalty = 0f;
            penalty += Mathf.Max(0f, _screenPadding - rect.xMin);
            penalty += Mathf.Max(0f, _screenPadding - rect.yMin);
            penalty += Mathf.Max(0f, rect.xMax - (Screen.width - _screenPadding));
            penalty += Mathf.Max(0f, rect.yMax - (Screen.height - _screenPadding));
            return penalty;
        }

        /// <summary>
        /// Calculate an adaptive tooltip position taking screen bounds and the cursor into account
        /// </summary>
        /// <param name="cursorPosition">Cursor position</param>
        /// <param name="tooltipSize">Tooltip size</param>
        /// <param name="tooltipPivot">Pivot tooltip</param>
        /// <param name="baseOffset">Base offset from the cursor</param>
        /// <returns>Optimal tooltip position</returns>
        private Vector2 CalculateAdaptivePosition(
            Vector2 cursorPosition,
            Vector2 tooltipSize,
            Vector2 tooltipPivot,
            Vector2 baseOffset,
            Rect? avoidRect = null)
        {
            // Copy the offset so it can be adjusted
            Vector2 adjustedOffset = baseOffset;

            // Step 1: Calculate the initial position
            Vector2 position = cursorPosition + adjustedOffset;

            // Step 2: Get the card bounds
            Rect bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);

            // Step 3: Adaptive horizontal flip
            if (bounds.xMax > Screen.width - _screenPadding)
            {
                // Does not fit on the right -> show it to the left of the cursor
                adjustedOffset.x = -Mathf.Abs(baseOffset.x) - tooltipSize.x * (1f - tooltipPivot.x);
            }
            else if (bounds.xMin < _screenPadding)
            {
                // Does not fit on the left -> show it to the right of the cursor
                adjustedOffset.x = Mathf.Abs(baseOffset.x) + tooltipSize.x * tooltipPivot.x;
            }

            // Step 4: Adaptive vertical flip
            if (bounds.yMax > Screen.height - _screenPadding)
            {
                // Does not fit above -> show it below the cursor
                adjustedOffset.y = -Mathf.Abs(baseOffset.y) - tooltipSize.y * (1f - tooltipPivot.y);
            }
            else if (bounds.yMin < _screenPadding)
            {
                // Does not fit below -> show it above the cursor
                adjustedOffset.y = Mathf.Abs(baseOffset.y) + tooltipSize.y * tooltipPivot.y;
            }

            // Step 5: Recalculate the position with the new offset
            position = cursorPosition + adjustedOffset;
            bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);

            // Step 6: Check for cursor overlap
            if (IsPointInRect(cursorPosition, bounds))
            {
                // The cursor overlaps the card, so it needs to be shifted
                float centerX = bounds.center.x;

                if (cursorPosition.x >= centerX)
                {
                    // Cursor is on the right side of the card -> move the card left
                    position.x = cursorPosition.x - tooltipSize.x - _cursorMargin - tooltipSize.x * tooltipPivot.x;
                }
                else
                {
                    // Cursor is on the left side of the card -> move the card right
                    position.x = cursorPosition.x + _cursorMargin + tooltipSize.x * (1f - tooltipPivot.x);
                }

                // Update bounds after shifting
                bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);
            }

            // Step 7: Keep the tooltip outside the hovered slot when slot bounds are available.
            if (avoidRect.HasValue && bounds.Overlaps(avoidRect.Value))
            {
                position = CalculatePositionOutsideRect(
                    avoidRect.Value,
                    cursorPosition,
                    tooltipSize,
                    tooltipPivot,
                    baseOffset);
                bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);
            }

            // Step 8: Final clamp (for very large cards or screen edges)
            // Keep the entire card visible on screen
            float clampedX = position.x;
            float clampedY = position.y;

            // Clamp X with pivot taken into account
            if (bounds.xMin < _screenPadding)
            {
                clampedX = _screenPadding + tooltipSize.x * tooltipPivot.x;
            }
            else if (bounds.xMax > Screen.width - _screenPadding)
            {
                clampedX = Screen.width - _screenPadding - tooltipSize.x * (1f - tooltipPivot.x);
            }

            // Clamp Y with pivot taken into account
            if (bounds.yMin < _screenPadding)
            {
                clampedY = _screenPadding + tooltipSize.y * tooltipPivot.y;
            }
            else if (bounds.yMax > Screen.height - _screenPadding)
            {
                clampedY = Screen.height - _screenPadding - tooltipSize.y * (1f - tooltipPivot.y);
            }

            return new Vector2(clampedX, clampedY);
        }

        private Vector2 CalculatePositionOutsideRect(
            Rect avoidRect,
            Vector2 anchorPosition,
            Vector2 tooltipSize,
            Vector2 tooltipPivot,
            Vector2 baseOffset)
        {
            var candidates = new[]
            {
                CreateCandidateRightOfRect(avoidRect, anchorPosition, tooltipSize, tooltipPivot),
                CreateCandidateLeftOfRect(avoidRect, anchorPosition, tooltipSize, tooltipPivot),
                CreateCandidateAboveRect(avoidRect, anchorPosition, tooltipSize, tooltipPivot),
                CreateCandidateBelowRect(avoidRect, anchorPosition, tooltipSize, tooltipPivot)
            };

            Vector2 best = candidates[0];
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                var bounds = GetTooltipScreenBounds(candidate, tooltipSize, tooltipPivot);
                float overlap = GetOverlapArea(bounds, avoidRect);
                float offscreen = GetOffscreenPenalty(bounds);
                float distance = Vector2.Distance(candidate, anchorPosition);
                float directionPenalty = GetDirectionPenalty(i, baseOffset);
                float fitBonus = IsRectInsideScreen(bounds, _screenPadding) ? -1000f : 0f;
                float score = overlap * 100000f + offscreen * 1000f + distance + directionPenalty + fitBonus;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private Vector2 CreateCandidateRightOfRect(Rect rect, Vector2 anchorPosition, Vector2 tooltipSize, Vector2 tooltipPivot)
        {
            return new Vector2(
                rect.xMax + _slotMargin + tooltipSize.x * tooltipPivot.x,
                anchorPosition.y);
        }

        private Vector2 CreateCandidateLeftOfRect(Rect rect, Vector2 anchorPosition, Vector2 tooltipSize, Vector2 tooltipPivot)
        {
            return new Vector2(
                rect.xMin - _slotMargin - tooltipSize.x * (1f - tooltipPivot.x),
                anchorPosition.y);
        }

        private Vector2 CreateCandidateAboveRect(Rect rect, Vector2 anchorPosition, Vector2 tooltipSize, Vector2 tooltipPivot)
        {
            return new Vector2(
                anchorPosition.x,
                rect.yMax + _slotMargin + tooltipSize.y * tooltipPivot.y);
        }

        private Vector2 CreateCandidateBelowRect(Rect rect, Vector2 anchorPosition, Vector2 tooltipSize, Vector2 tooltipPivot)
        {
            return new Vector2(
                anchorPosition.x,
                rect.yMin - _slotMargin - tooltipSize.y * (1f - tooltipPivot.y));
        }

        private static float GetDirectionPenalty(int candidateIndex, Vector2 baseOffset)
        {
            const float preferredDirectionBonus = -100f;
            switch (candidateIndex)
            {
                case 0:
                    return baseOffset.x >= 0f ? preferredDirectionBonus : 0f;
                case 1:
                    return baseOffset.x < 0f ? preferredDirectionBonus : 0f;
                case 2:
                    return baseOffset.y >= 0f ? preferredDirectionBonus : 0f;
                case 3:
                    return baseOffset.y < 0f ? preferredDirectionBonus : 0f;
                default:
                    return 0f;
            }
        }

        private void RefreshTooltipLayout()
        {
            if (!_tooltipLayoutDirty)
                return;

            var rectTransform = _currentBaseTooltipView != null ? _currentBaseTooltipView.rectTransform : null;
            if (rectTransform == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();
            _tooltipLayoutDirty = false;
        }

        private Vector2 GetTooltipScreenSize()
        {
            var rectTransform = _currentBaseTooltipView != null ? _currentBaseTooltipView.rectTransform : null;
            if (rectTransform == null)
                return Vector2.zero;

            if (TryGetRectScreenBounds(rectTransform, out var bounds) && bounds.size.sqrMagnitude > 0f)
                return bounds.size;

            return _currentBaseTooltipView.GetSize();
        }

        private static Vector2 GetPointInRect(Rect rect, Vector2 normalizedPoint)
        {
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalizedPoint.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalizedPoint.y));
        }

        private static bool TryGetSlotScreenBounds(RectTransform slotRectTransform, out Rect bounds)
        {
            return TryGetRectScreenBounds(slotRectTransform, out bounds);
        }

        private static bool TryGetRectScreenBounds(RectTransform rectTransform, out Rect bounds)
        {
            bounds = default;
            if (rectTransform == null)
                return false;

            var worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);
            var camera = Extensions.GetCanvasCamera(rectTransform);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < worldCorners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            if (float.IsInfinity(min.x) || float.IsInfinity(min.y) || float.IsInfinity(max.x) || float.IsInfinity(max.y))
                return false;

            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        #endregion

        /// <summary>
        /// Stop all tooltip coroutines
        /// </summary>
        private void StopAllTooltipCoroutines()
        {
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }
        }

        enum TooltipAnchor
        {
            Cursor,
            SlotPivot
        }
    }
}
