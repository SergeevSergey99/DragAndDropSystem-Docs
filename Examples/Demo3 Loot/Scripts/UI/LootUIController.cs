using DragAndDropSystem.Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
{
    /// <summary>
    /// Контроллер UI лута - медиатор между игровым миром и UI.
    /// ЕДИНСТВЕННЫЙ компонент который знает о UI и управляет им.
    /// Реагирует на события от PlayerInteraction и Chest, управляет показом/скрытием UI.
    /// </summary>
    public class LootUIController : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField, Tooltip("Панель с инвентарями (изначально выключена)")]
        private GameObject _lootPanel;
        [SerializeField]
        private GameObject _interactableButtonPanel;

        [Header("Data Bindings")]

        [SerializeField, Tooltip("DataBinding инвентаря сундука")]
        private ChestInventoryDataBinding _chestBinding;

        [Header("Settings")]
        [SerializeField, Tooltip("Клавиша закрытия UI")]
        private KeyCode _closeKey = KeyCode.Escape;

        [SerializeField, Tooltip("Автоматически закрывать UI когда игрок отходит от сундука")]
        private bool _autoCloseOnDistanceExit = true;

        private Chest _currentChest;
        private bool _isLootUIOpen = false;
        
        private PlayerInteraction _playerInteraction;
        private PlayerController _playerController;

        private void Awake()
        {
            // Убеждаемся что панель лута изначально выключена
            if (_lootPanel != null)
            {
                _lootPanel.SetActive(false);
            }
            // Автоматически находим компоненты если они не назначены
            if (_playerInteraction == null)
            {
                _playerInteraction = FindObjectOfType<PlayerInteraction>();
            }

            if (_playerController == null)
            {
                _playerController = FindObjectOfType<PlayerController>();
            }
        }

        private void OnEnable()
        {
            // Подписываемся на события игрока
            if (_playerInteraction != null)
            {
                _playerInteraction.OnInteracted += OnPlayerInteracted;
                _playerInteraction.OnInteractableEntered += ShowInteractableButtonPanel;
                _playerInteraction.OnInteractableExited += HideInteractableButtonPanel;

                if (_autoCloseOnDistanceExit)
                {
                    _playerInteraction.OnInteractableExited += OnInteractableExited;
                }
            }
        }
        private void ShowInteractableButtonPanel(IInteractable interactable) => _interactableButtonPanel?.SetActive(true);
        private void HideInteractableButtonPanel(IInteractable interactable) => _interactableButtonPanel?.SetActive(false);

        private void OnDisable()
        {
            // Отписываемся от событий
            if (_playerInteraction != null)
            {
                _playerInteraction.OnInteracted -= OnPlayerInteracted;
                _playerInteraction.OnInteractableEntered -= ShowInteractableButtonPanel;
                _playerInteraction.OnInteractableExited -= HideInteractableButtonPanel;

                if (_autoCloseOnDistanceExit)
                {
                    _playerInteraction.OnInteractableExited -= OnInteractableExited;
                }
            }

            // Отвязываемся от сундука если был открыт
            if (_currentChest != null)
            {
                UnsubscribeFromChestEvents(_currentChest);
            }
        }

        private void Update()
        {
            // Обрабатываем клавишу закрытия
            if (_isLootUIOpen && Input.GetKeyDown(_closeKey))
            {
                CloseLootUI();
            }
        }

        /// <summary>
        /// Обработчик взаимодействия игрока с объектом
        /// </summary>
        private void OnPlayerInteracted(IInteractable interactable)
        {
            Debug.Log($"[LootUIController] Player interacted");
            // Проверяем что взаимодействие было с сундуком
            if (interactable is Chest chest)
            {
                // Если сундук открылся - показываем UI
                if (chest.IsOpen)
                {
                    OpenLootUI(chest);
                    HideInteractableButtonPanel(interactable);
                }
                // Если сундук закрылся - скрываем UI
                else
                {
                    CloseLootUI();
                }
            }
        }

        /// <summary>
        /// Обработчик выхода из зоны взаимодействия
        /// </summary>
        private void OnInteractableExited(IInteractable interactable)
        {
            // Если UI открыт и игрок отошел от сундука - закрываем
            if (_isLootUIOpen && _autoCloseOnDistanceExit)
            {
                Debug.Log("[LootUIController] Player left interaction zone, closing loot UI");
                CloseLootUI();
            }
        }

        /// <summary>
        /// Открыть UI лута для конкретного сундука
        /// </summary>
        private void OpenLootUI(Chest chest)
        {
            if (chest == null)
            {
                Debug.LogWarning("[LootUIController] Cannot open loot UI - chest is null");
                return;
            }

            Debug.Log($"[LootUIController] Opening loot UI for chest '{chest.gameObject.name}'");

            _currentChest = chest;
            _isLootUIOpen = true;

            // Подписываемся на события сундука
            SubscribeToChestEvents(chest);

            // Привязываем данные сундука к UI
            _chestBinding?.BindToChest(chest);

            // Показываем панель
            _lootPanel.SetActive(true);

            // Блокируем управление игроком
            if (_playerController != null)
            {
                _playerController.SetInputLocked(true);
            }
        }

        /// <summary>
        /// Закрыть UI лута
        /// </summary>
        [Button("Close Loot UI"), DisableInEditorMode]
        public void CloseLootUI()
        {
            if (!_isLootUIOpen)
                return;

            Debug.Log("[LootUIController] Closing loot UI");

            // Закрываем сундук если он был открыт
            if (_currentChest != null && _currentChest.IsOpen)
            {
                // Используем Interact чтобы корректно закрыть сундук
                _currentChest.Interact(_playerInteraction);
            }

            // Отписываемся от событий сундука
            if (_currentChest != null)
            {
                UnsubscribeFromChestEvents(_currentChest);
            }

            // Отвязываем данные
            _chestBinding?.BindToChest(null);

            // Скрываем панель
            _lootPanel.SetActive(false);

            // Разблокируем управление игроком
            if (_playerController != null)
            {
                _playerController.SetInputLocked(false);
            }

            _currentChest = null;
            _isLootUIOpen = false;
        }

        /// <summary>
        /// Подписаться на события сундука
        /// </summary>
        private void SubscribeToChestEvents(Chest chest)
        {
            if (chest == null)
                return;

            chest.OnChestClosed += OnChestClosedExternally;
        }

        /// <summary>
        /// Отписаться от событий сундука
        /// </summary>
        private void UnsubscribeFromChestEvents(Chest chest)
        {
            if (chest == null)
                return;

            chest.OnChestClosed -= OnChestClosedExternally;
        }

        /// <summary>
        /// Обработчик закрытия сундука извне (не через кнопку Close)
        /// </summary>
        private void OnChestClosedExternally(Chest chest)
        {
            Debug.Log("[LootUIController] Chest was closed externally");
            // Закрываем UI без повторного вызова Interact на сундуке

            if (_currentChest != null)
            {
                UnsubscribeFromChestEvents(_currentChest);
            }

            _chestBinding?.BindToChest(null);
            _lootPanel.SetActive(false);

            if (_playerController != null)
            {
                _playerController.SetInputLocked(false);
            }

            _currentChest = null;
            _isLootUIOpen = false;
        }
    }
}
