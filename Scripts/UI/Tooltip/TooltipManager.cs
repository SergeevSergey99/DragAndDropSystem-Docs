using System.Collections;
using UnityEngine;
using UniversalDragAndDrop.Interaction;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.UI
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

        [Header("Timing")]
        [SerializeField, Tooltip("Delay before showing the tooltip (seconds)")]
        private float _showDelay = 0.5f;
        
        private Coroutine _showCoroutine;
        private SlotHoverEventArgs _currentHoverArgs;
        private BaseTooltipView _currentBaseTooltipView;


        private void OnEnable()
        {
            // Subscribe to global static slot events
            SlotInputAdapter.OnAnySlotHoverEnter += OnSlotHoverEnter;
            SlotInputAdapter.OnAnySlotHoverExit += OnSlotHoverExit;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            SlotInputAdapter.OnAnySlotHoverEnter -= OnSlotHoverEnter;
            SlotInputAdapter.OnAnySlotHoverExit -= OnSlotHoverExit;

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

            // Use adaptive positioning
            position = CalculateAdaptivePosition(
                args.ScreenPosition,
                _currentBaseTooltipView.GetSize(),
                _currentBaseTooltipView.rectTransform.pivot,
                _offset
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
            Vector2 baseOffset)
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

            // Step 7: Final clamp (for very large cards or screen edges)
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