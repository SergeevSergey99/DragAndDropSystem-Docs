using System;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Опциональный компонент для отслеживания наведения курсора на слот.
    ///
    /// Предоставляет три способа подписки на события наведения:
    /// 1. Статические события (OnAnySlotHoverEnter/Exit) - для глобальных систем (TooltipManager, SoundManager и т.д.)
    /// 2. UnityEvents (onSlotHoverEnter/Exit) - для настройки в Inspector
    /// 3. Виртуальные методы (OnSlotHoverEnterInternal/ExitInternal) - для переопределения в наследниках
    ///
    /// Добавьте этот компонент на слот если нужна функциональность tooltip, звуки при наведении и т.д.
    /// </summary>
    [RequireComponent(typeof(UniversalSlot))]
    public class SlotHoverEventListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Static Events (Global Systems)

        /// <summary>
        /// Глобальное событие наведения на любой слот (для TooltipManager, SoundManager и т.д.)
        /// </summary>
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverEnter;

        /// <summary>
        /// Глобальное событие ухода курсора с любого слота
        /// </summary>
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverExit;

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField, Tooltip("Слот для отслеживания наведения")]
        private UniversalSlot _slot;

        [Header("Filtering")]
        [SerializeField, Tooltip("Вызывать события только если слот не пустой")]
        private bool _onlyWhenNotEmpty = true;

        [SerializeField, Tooltip("Игнорировать события если идет перетаскивание")]
        private bool _ignoreWhileDragging = false;

        [Header("Local Events (Optional)")]
        [SerializeField, Tooltip("Событие наведения на слот (настраивается в Inspector)")]
        private UnityEvent<SlotHoverEventArgs> onSlotHoverEnter = new UnityEvent<SlotHoverEventArgs>();

        [SerializeField, Tooltip("Событие ухода курсора со слота (настраивается в Inspector)")]
        private UnityEvent<SlotHoverEventArgs> onSlotHoverExit = new UnityEvent<SlotHoverEventArgs>();

        #endregion

        #region Private Fields

        private bool _isHovering = false;

        #endregion

        #region Properties

        /// <summary>
        /// Слот к которому привязан этот listener
        /// </summary>
        public UniversalSlot Slot => _slot;

        /// <summary>
        /// Находится ли курсор над слотом в данный момент
        /// </summary>
        public bool IsHovering => _isHovering;

        /// <summary>
        /// UnityEvent для наведения (для настройки в Inspector)
        /// </summary>
        public UnityEvent<SlotHoverEventArgs> OnSlotHoverEnter => onSlotHoverEnter;

        /// <summary>
        /// UnityEvent для ухода (для настройки в Inspector)
        /// </summary>
        public UnityEvent<SlotHoverEventArgs> OnSlotHoverExit => onSlotHoverExit;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Автоматически находим слот если не назначен
            if (_slot == null)
            {
                _slot = GetComponent<UniversalSlot>();
            }

            if (_slot == null)
            {
                Debug.LogError($"[SlotHoverEventListener] UniversalSlot not found on {gameObject.name}! This component requires UniversalSlot.", this);
            }
        }

        private void OnDisable()
        {
            // При отключении вызываем exit если был hovering
            if (_isHovering)
            {
                ForceExit();
            }
        }

        #endregion

        #region IPointerEnterHandler Implementation

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Проверяем базовые условия
            if (!ShouldTriggerEvent()) return;

            _isHovering = true;

            // Создаем аргументы события
            var args = CreateEventArgs(eventData, true);

            // 1. Вызываем виртуальный метод (наследники могут переопределить)
            OnSlotHoverEnterInternal(args);

            // Если событие отменено, не вызываем дальнейшие обработчики
            if (args.Cancel) return;

            // 2. Вызываем локальное UnityEvent (для Inspector настройки)
            try
            {
                onSlotHoverEnter?.Invoke(args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlotHoverEventListener] Error in local onSlotHoverEnter: {ex.Message}", this);
            }

            // 3. Вызываем глобальное статическое событие (для глобальных систем)
            try
            {
                OnAnySlotHoverEnter?.Invoke(args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlotHoverEventListener] Error in OnAnySlotHoverEnter: {ex.Message}", this);
            }
        }

        #endregion

        #region IPointerExitHandler Implementation

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isHovering)
                return;

            _isHovering = false;

            // Создаем аргументы события
            var args = CreateEventArgs(eventData, false);

            // 1. Вызываем виртуальный метод (наследники могут переопределить)
            OnSlotHoverExitInternal(args);

            // Если событие отменено, не вызываем дальнейшие обработчики
            if (args.Cancel) return;

            // 2. Вызываем локальное UnityEvent
            try
            {
                onSlotHoverExit?.Invoke(args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlotHoverEventListener] Error in local onSlotHoverExit: {ex.Message}", this);
            }

            // 3. Вызываем глобальное статическое событие
            try
            {
                OnAnySlotHoverExit?.Invoke(args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SlotHoverEventListener] Error in OnAnySlotHoverExit: {ex.Message}", this);
            }
        }

        #endregion

        #region Virtual Methods (For Inheritance)

        /// <summary>
        /// Виртуальный метод вызываемый при наведении курсора на слот.
        /// Переопределите в наследнике для кастомной логики.
        /// Установите args.Cancel = true чтобы отменить дальнейшую обработку события.
        /// </summary>
        /// <param name="args">Аргументы события наведения</param>
        protected virtual void OnSlotHoverEnterInternal(SlotHoverEventArgs args)
        {
            // Базовая реализация пустая - переопределите в наследнике
        }

        /// <summary>
        /// Виртуальный метод вызываемый при уходе курсора со слота.
        /// Переопределите в наследнике для кастомной логики.
        /// Установите args.Cancel = true чтобы отменить дальнейшую обработку события.
        /// </summary>
        /// <param name="args">Аргументы события ухода</param>
        protected virtual void OnSlotHoverExitInternal(SlotHoverEventArgs args)
        {
            // Базовая реализация пустая - переопределите в наследнике
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Проверить должны ли мы вызывать события
        /// </summary>
        private bool ShouldTriggerEvent()
        {
            if (_slot == null)
                return false;

            // Если слот неинтерактивен (отфильтрован)
            if (!_slot.IsInteractable)
                return false;

            // Если нужно только для непустых слотов
            if (_onlyWhenNotEmpty && _slot.IsEmpty)
                return false;

            // Если нужно игнорировать во время перетаскивания
            if (_ignoreWhileDragging && DragAndDropManager.IsInstanceExist && DragAndDropManager.Instance.IsDragging)
                return false;

            return true;
        }

        /// <summary>
        /// Создать аргументы события
        /// </summary>
        private SlotHoverEventArgs CreateEventArgs(PointerEventData eventData, bool isEnter)
        {
            return new SlotHoverEventArgs(
                _slot.Stack?.Item,
                _slot,
                eventData?.position ?? Vector2.zero,
                GetComponent<RectTransform>(),
                isEnter
            );
        }

        /// <summary>
        /// Принудительно вызвать событие выхода (используется при отключении)
        /// </summary>
        private void ForceExit()
        {
            _isHovering = false;

            var args = new SlotHoverEventArgs(
                _slot?.Stack?.Item,
                _slot,
                Vector2.zero,
                GetComponent<RectTransform>(),
                false
            );

            // Вызываем только если есть подписчики или виртуальный метод переопределен
            OnSlotHoverExitInternal(args);

            if (!args.Cancel)
            {
                onSlotHoverExit?.Invoke(args);
                OnAnySlotHoverExit?.Invoke(args);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Получить текущее состояние наведения
        /// </summary>
        public bool GetIsHovering() => _isHovering;

        /// <summary>
        /// Программно симулировать наведение (для тестирования)
        /// </summary>
        public void SimulateHoverEnter()
        {
            if (_isHovering)
                return;

            OnPointerEnter(new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            });
        }

        /// <summary>
        /// Программно симулировать уход (для тестирования)
        /// </summary>
        public void SimulateHoverExit()
        {
            if (!_isHovering)
                return;

            OnPointerExit(new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            });
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Автоматически находим слот
            if (_slot == null)
            {
                _slot = GetComponent<UniversalSlot>();
            }
        }

        private void Reset()
        {
            // При добавлении компонента автоматически находим слот
            _slot = GetComponent<UniversalSlot>();
            _onlyWhenNotEmpty = true;
            _ignoreWhileDragging = false;
        }
#endif

        #endregion
    }
}
