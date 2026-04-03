using System;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo2Loot
{
    /// <summary>
    /// Система взаимодействия игрока с объектами в мире.
    /// НЕ знает о UI, только определяет с чем можно взаимодействовать и вызывает события.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField, Tooltip("Радиус поиска интерактивных объектов")]
        private float _interactionRadius = 2f;

        [SerializeField, Tooltip("Слой интерактивных объектов")]
        private LayerMask _interactableLayer = -1; // По умолчанию все слои

        [SerializeField, Tooltip("Клавиша взаимодействия")]
        private KeyCode _interactKey = KeyCode.E;

        // Components
        private PlayerController _playerController;

        // State
        private IInteractable _currentInteractable;
        private Collider2D[] _overlapResults = new Collider2D[10]; // Буфер для результатов поиска
        ContactFilter2D _contactFilter;
        // Events (UI подписывается на эти события)
        /// <summary>
        /// Игрок вошел в зону взаимодействия с объектом
        /// </summary>
        public event Action<IInteractable> OnInteractableEntered;

        /// <summary>
        /// Игрок вышел из зоны взаимодействия
        /// </summary>
        public event Action<IInteractable> OnInteractableExited;

        /// <summary>
        /// Игрок взаимодействовал с объектом
        /// </summary>
        public event Action<IInteractable> OnInteracted;

        public PlayerInventoryData Inventory { get; private set; }
        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            Inventory = FindAnyObjectByType<PlayerInventoryData>();
            
            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(_interactableLayer);
            _contactFilter.useTriggers = true;
        }

        private void Update()
        {
            // Ищем интерактивные объекты поблизости
            FindNearestInteractable();

            // Обрабатываем ввод
            HandleInteractionInput();
        }

        private void FindNearestInteractable()
        {
            // Если управление заблокировано (например, открыто UI), не ищем
            if (_playerController != null && _playerController.InputLocked)
            {
                return;
            }

            // Ищем коллайдеры в радиусе
            int count = Physics2D.OverlapCircle(
                transform.position,
                _interactionRadius,
                _contactFilter,
                _overlapResults
            );
            
            IInteractable nearest = null;
            float nearestDistance = float.MaxValue;

            // Находим ближайший интерактивный объект
            for (int i = 0; i < count; i++)
            {
                var collider = _overlapResults[i];
                if (collider == null || collider.gameObject == gameObject)
                    continue;

                var interactable = collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    float distance = Vector2.SqrMagnitude(transform.position - collider.transform.position);

                    if (distance < nearestDistance)
                    {
                        nearest = interactable;
                        nearestDistance = distance;
                    }
                }
            }

            // Обновляем текущий объект для взаимодействия
            UpdateCurrentInteractable(nearest);
        }

        private void UpdateCurrentInteractable(IInteractable newInteractable)
        {
            // Если объект не изменился, ничего не делаем
            if (_currentInteractable == newInteractable)
                return;
            
            if (newInteractable != null && !newInteractable.CanInteract(this)) return;
            
            // Если был старый объект, вызываем событие выхода
            if (_currentInteractable != null)
            {
                OnInteractableExited?.Invoke(_currentInteractable);
            }

            // Устанавливаем новый объект
            _currentInteractable = newInteractable;
            Debug.Log($"Current interactable updated to: {_currentInteractable}");

            // Если есть новый объект, вызываем событие входа
            if (_currentInteractable != null)
            {
                
                OnInteractableEntered?.Invoke(_currentInteractable);
            }
        }

        private void HandleInteractionInput()
        {
            // Если нет объекта для взаимодействия, выходим
            if (_currentInteractable == null)
                return;

            // Если нажата клавиша взаимодействия
            if (Input.GetKeyDown(_interactKey))
            {
                // Вызываем взаимодействие на объекте
                _currentInteractable.Interact(this);

                // Вызываем событие (UI подпишется и покажет/скроет окно)
                OnInteracted?.Invoke(_currentInteractable);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Показываем радиус взаимодействия
            Gizmos.color = _currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);

            // Если есть текущий объект, рисуем линию к нему
            if (_currentInteractable != null)
            {
                var interactableObj = _currentInteractable as MonoBehaviour;
                if (interactableObj != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, interactableObj.transform.position);
                }
            }
        }
    }
}