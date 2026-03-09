using System.Collections;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Slots;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Менеджер tooltip для отображения информации о предметах при наведении на слоты.
    /// ОПЦИОНАЛЬНЫЙ компонент - работает только если добавлен на сцену.
    /// Подписывается на статические hover-события SlotInputAdapter.
    ///
    /// Использует ITooltipView для визуализации - можно указать разные префабы tooltip для разных предметов.
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        [SerializeField, Required]
        private Canvas _canvas;
        [Header("Default Tooltip View")]
        [SerializeField, Required, Tooltip("Дефолтный префаб tooltip (должен реализовывать ITooltipView)")]
        private BaseTooltipView _defaultTooltipPrefab;

        [Header("Positioning")]
        [SerializeField, Tooltip("Смещение tooltip от курсора")]
        private Vector2 _offset = new Vector2(15, -15);

        [SerializeField, Tooltip("Якорь позиционирования tooltip")]
        private TooltipAnchor _anchor = TooltipAnchor.Cursor;

        [SerializeField, Tooltip("Отступы от краев экрана")]
        private float _screenPadding = 10f;

        [SerializeField, Tooltip("Минимальное расстояние между курсором и карточкой")]
        private float _cursorMargin = 5f;

        [Header("Timing")]
        [SerializeField, Tooltip("Задержка перед показом tooltip (секунды)")]
        private float _showDelay = 0.5f;
        
        private Coroutine _showCoroutine;
        private SlotHoverEventArgs _currentHoverArgs;
        private BaseTooltipView _currentBaseTooltipView;


        private void OnEnable()
        {
            // Подписываемся на глобальные статические события слотов
            SlotInputAdapter.OnAnySlotHoverEnter += OnSlotHoverEnter;
            SlotInputAdapter.OnAnySlotHoverExit += OnSlotHoverExit;
        }

        private void OnDisable()
        {
            // Отписываемся от событий
            SlotInputAdapter.OnAnySlotHoverEnter -= OnSlotHoverEnter;
            SlotInputAdapter.OnAnySlotHoverExit -= OnSlotHoverExit;

            // Останавливаем корутины
            StopAllTooltipCoroutines();

            // Скрываем tooltip
            HideTooltip();
        }

        private void Update()
        {
            // Если tooltip виден и привязан к курсору, обновляем позицию
            if (_anchor == TooltipAnchor.Cursor && _currentBaseTooltipView != null)
            {
                _currentHoverArgs.ScreenPosition = Input.mousePosition;
                UpdateTooltipPosition(_currentHoverArgs);
            }
        }

        #region Event Handlers

        /// <summary>
        /// Обработчик наведения на слот
        /// </summary>
        private void OnSlotHoverEnter(SlotHoverEventArgs args)
        {
            // Игнорируем если нет предмета
            if (!args.HasItem) return;

            _currentHoverArgs = args;

            // Отменяем предыдущий показ если был
            StopAllTooltipCoroutines();

            // Показываем с задержкой
            _showCoroutine = StartCoroutine(ShowTooltipDelayed(args));
        }

        /// <summary>
        /// Обработчик ухода курсора со слота
        /// </summary>
        private void OnSlotHoverExit(SlotHoverEventArgs args)
        {
            // Отменяем показ если еще не показали
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }

            // Скрываем tooltip
            HideTooltip();
        }

        #endregion

        #region Tooltip Display

        /// <summary>
        /// Показать tooltip с задержкой
        /// </summary>
        private IEnumerator ShowTooltipDelayed(SlotHoverEventArgs args)
        {
            yield return new WaitForSeconds(_showDelay);
            ShowTooltip(args);
        }

        /// <summary>
        /// Показать tooltip
        /// </summary>
        private void ShowTooltip(SlotHoverEventArgs args)
        {
            if (args.Item == null) return;

            // Скрываем предыдущий tooltip если был
            if (_currentBaseTooltipView != null)
            {
                HideTooltip();
            }

            // Получаем или создаем tooltip view
            _currentBaseTooltipView = Instantiate(_defaultTooltipPrefab, _canvas.transform);

            if (_currentBaseTooltipView == null)
            {
                Debug.LogError("[TooltipManager] Failed to get tooltip view!");
                return;
            }

            // Показываем tooltip с содержимым
            _currentBaseTooltipView.Show(args.Item);

            // Позиционируем
            UpdateTooltipPosition(args);
        }

        /// <summary>
        /// Скрыть tooltip
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
        /// Обновить позицию tooltip
        /// </summary>
        private void UpdateTooltipPosition(SlotHoverEventArgs args)
        {
            if (_currentBaseTooltipView == null || args == null)
                return;

            Vector2 position;

            // Используем адаптивное позиционирование
            position = CalculateAdaptivePosition(
                args.ScreenPosition,
                _currentBaseTooltipView.GetSize(),
                _currentBaseTooltipView.rectTransform.pivot,
                _offset
            );

            _currentBaseTooltipView.UpdatePosition(position);
        }

        /// <summary>
        /// Получить bounds карточки в экранных координатах
        /// </summary>
        /// <param name="position">Позиция карточки (anchor точка)</param>
        /// <param name="size">Размер карточки</param>
        /// <param name="pivot">Pivot карточки (0,0 = левый нижний угол, 1,1 = правый верхний)</param>
        /// <returns>Rect в экранных координатах</returns>
        private Rect GetTooltipScreenBounds(Vector2 position, Vector2 size, Vector2 pivot)
        {
            float left = position.x - size.x * pivot.x;
            float bottom = position.y - size.y * pivot.y;
            return new Rect(left, bottom, size.x, size.y);
        }

        /// <summary>
        /// Проверить попадание точки в прямоугольник
        /// </summary>
        private bool IsPointInRect(Vector2 point, Rect rect)
        {
            return rect.Contains(point);
        }

        /// <summary>
        /// Рассчитать адаптивную позицию tooltip с учетом границ экрана и курсора
        /// </summary>
        /// <param name="cursorPosition">Позиция курсора</param>
        /// <param name="tooltipSize">Размер tooltip</param>
        /// <param name="tooltipPivot">Pivot tooltip</param>
        /// <param name="baseOffset">Базовое смещение от курсора</param>
        /// <returns>Оптимальная позиция tooltip</returns>
        private Vector2 CalculateAdaptivePosition(
            Vector2 cursorPosition,
            Vector2 tooltipSize,
            Vector2 tooltipPivot,
            Vector2 baseOffset)
        {
            // Копируем offset чтобы его можно было изменять
            Vector2 adjustedOffset = baseOffset;

            // Шаг 1: Рассчитываем начальную позицию
            Vector2 position = cursorPosition + adjustedOffset;

            // Шаг 2: Получаем bounds карточки
            Rect bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);

            // Шаг 3: Adaptive флип по горизонтали
            if (bounds.xMax > Screen.width - _screenPadding)
            {
                // Не влезает справа → показываем слева от курсора
                adjustedOffset.x = -Mathf.Abs(baseOffset.x) - tooltipSize.x * (1f - tooltipPivot.x);
            }
            else if (bounds.xMin < _screenPadding)
            {
                // Не влезает слева → показываем справа от курсора
                adjustedOffset.x = Mathf.Abs(baseOffset.x) + tooltipSize.x * tooltipPivot.x;
            }

            // Шаг 4: Adaptive флип по вертикали
            if (bounds.yMax > Screen.height - _screenPadding)
            {
                // Не влезает сверху → показываем снизу от курсора
                adjustedOffset.y = -Mathf.Abs(baseOffset.y) - tooltipSize.y * (1f - tooltipPivot.y);
            }
            else if (bounds.yMin < _screenPadding)
            {
                // Не влезает снизу → показываем сверху от курсора
                adjustedOffset.y = Mathf.Abs(baseOffset.y) + tooltipSize.y * tooltipPivot.y;
            }

            // Шаг 5: Пересчитываем позицию с новым offset
            position = cursorPosition + adjustedOffset;
            bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);

            // Шаг 6: Проверяем пересечение с курсором
            if (IsPointInRect(cursorPosition, bounds))
            {
                // Курсор попадает в карточку - нужно сдвинуть её
                float centerX = bounds.center.x;

                if (cursorPosition.x >= centerX)
                {
                    // Курсор в правой части карточки → сдвигаем карточку влево
                    position.x = cursorPosition.x - tooltipSize.x - _cursorMargin - tooltipSize.x * tooltipPivot.x;
                }
                else
                {
                    // Курсор в левой части карточки → сдвигаем карточку вправо
                    position.x = cursorPosition.x + _cursorMargin + tooltipSize.x * (1f - tooltipPivot.x);
                }

                // Обновляем bounds после сдвига
                bounds = GetTooltipScreenBounds(position, tooltipSize, tooltipPivot);
            }

            // Шаг 7: Финальный clamp (на случай очень больших карточек или края экрана)
            // Ограничиваем так чтобы карточка была полностью на экране
            float clampedX = position.x;
            float clampedY = position.y;

            // Clamp по X с учетом pivot
            if (bounds.xMin < _screenPadding)
            {
                clampedX = _screenPadding + tooltipSize.x * tooltipPivot.x;
            }
            else if (bounds.xMax > Screen.width - _screenPadding)
            {
                clampedX = Screen.width - _screenPadding - tooltipSize.x * (1f - tooltipPivot.x);
            }

            // Clamp по Y с учетом pivot
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

        #region Utility

        /// <summary>
        /// Остановить все корутины tooltip
        /// </summary>
        private void StopAllTooltipCoroutines()
        {
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }
        }

        #endregion

        #region Data Structures

        public enum TooltipAnchor
        {
            Cursor,
            SlotTopRight,
            SlotTopLeft,
            SlotBottomRight,
            SlotBottomLeft
        }

        #endregion
    }
}
