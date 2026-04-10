using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// Контроллер UI лута - медиатор между игровым миром и UI.
    /// ЕДИНСТВЕННЫЙ компонент который знает о UI и управляет им.
    /// Реагирует на события от PlayerInteraction и Chest, управляет показом/скрытием UI.
    /// </summary>
    public class LootUIController : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField, Tooltip("Inventory panel")]
        private GameObject _inventoryPanel;
        [SerializeField, Tooltip("Panel with inventories and loot")]
        private GameObject _lootPanel;
        [SerializeField]
        private GameObject _interactableButtonPanel;

        [Header("Data Bindings")]

        [SerializeField, Tooltip("Chest inventory DataBinding")]
        private ChestInventoryDataBinding _chestBinding;

        [Header("Settings")]
        [SerializeField, Tooltip("Key to open/close the player's regular inventory")]
        private KeyCode _toggleInventoryKey = KeyCode.I;

        [SerializeField, Tooltip("UI close key")]
        private KeyCode _closeKey = KeyCode.Escape;

        [SerializeField, Tooltip("Automatically close the UI when the player moves away from the chest")]
        private bool _autoCloseOnDistanceExit = true;

        private Chest _currentChest;
        private bool _isLootUIOpen = false;
        private bool _isInventoryUIOpen = false;
        
        private PlayerInteraction _playerInteraction;
        private PlayerController _playerController;

        private void Awake()
        {
            // Убеждаемся что панель лута изначально выключена
            if (_lootPanel != null)
            {
                _lootPanel.transform.localPosition = Vector3.zero;
                _lootPanel.SetActive(false);
            }

            if (_interactableButtonPanel != null)
            {
                _interactableButtonPanel.transform.localPosition = Vector3.zero;
                _interactableButtonPanel.SetActive(false);
            }
            
            if (_inventoryPanel != null)
            {
                _inventoryPanel.transform.localPosition = Vector3.zero;
                _inventoryPanel.SetActive(false);
            }
            
            // Автоматически находим компоненты если они не назначены
            if (_playerInteraction == null)
            {
                _playerInteraction = Object.FindFirstObjectByType<PlayerInteraction>();
            }

            if (_playerController == null)
            {
                _playerController = Object.FindFirstObjectByType<PlayerController>();
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
            if (!_isLootUIOpen && Input.GetKeyDown(_toggleInventoryKey))
            {
                ToggleInventoryUI();
            }

            // Обрабатываем клавишу закрытия
            if ((_isLootUIOpen || _isInventoryUIOpen) && Input.GetKeyDown(_closeKey))
            {
                if (_isLootUIOpen)
                    CloseLootUI();
                else
                    CloseInventoryUI();
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
            _isInventoryUIOpen = false;

            // Подписываемся на события сундука
            SubscribeToChestEvents(chest);

            // Привязываем данные сундука к UI
            _chestBinding?.BindToChest(chest);

            // Показываем панель
            _lootPanel?.SetActive(true);
            _inventoryPanel?.SetActive(false);
            _interactableButtonPanel?.SetActive(false);
            RefreshInputLockState();
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
            _lootPanel?.SetActive(false);

            _currentChest = null;
            _isLootUIOpen = false;
            RefreshInputLockState();
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
            _lootPanel?.SetActive(false);

            _currentChest = null;
            _isLootUIOpen = false;
            RefreshInputLockState();
        }

        private void ToggleInventoryUI()
        {
            if (_inventoryPanel == null)
            {
                Debug.LogWarning("[LootUIController] Cannot toggle inventory UI - panel is not assigned");
                return;
            }

            if (_isInventoryUIOpen)
                CloseInventoryUI();
            else
                OpenInventoryUI();
        }

        private void OpenInventoryUI()
        {
            if (_inventoryPanel == null)
            {
                Debug.LogWarning("[LootUIController] Cannot open inventory UI - panel is not assigned");
                return;
            }

            _inventoryPanel.SetActive(true);
            _interactableButtonPanel?.SetActive(false);
            _isInventoryUIOpen = true;
            RefreshInputLockState();
        }

        private void CloseInventoryUI()
        {
            if (!_isInventoryUIOpen || _isLootUIOpen)
                return;

            _inventoryPanel?.SetActive(false);
            _isInventoryUIOpen = false;
            RefreshInputLockState();
        }

        private void RefreshInputLockState()
        {
            if (_playerController != null)
                _playerController.SetInputLocked(_isLootUIOpen || _isInventoryUIOpen);
        }
    }
}
