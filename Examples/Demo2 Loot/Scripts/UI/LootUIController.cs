using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Examples.Loot
{
    /// <summary>
    /// Loot UI controller, acting as a mediator between the game world and UI.
    /// The ONLY component that knows about the UI and manages it.
    /// Reacts to PlayerInteraction and Chest events and controls UI show/hide behavior.
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
            // Ensure the loot panel starts disabled
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
            
            // Automatically find components if they are not assigned
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
            // Subscribe to player events
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
            // Unsubscribe from events
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

            // Unbind from the chest if one was open
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

            // Handle the close key
            if ((_isLootUIOpen || _isInventoryUIOpen) && Input.GetKeyDown(_closeKey))
            {
                if (_isLootUIOpen)
                    CloseLootUI();
                else
                    CloseInventoryUI();
            }
        }

        /// <summary>
        /// Handler for player interaction with an object
        /// </summary>
        private void OnPlayerInteracted(IInteractable interactable)
        {
            Debug.Log($"[LootUIController] Player interacted");
            // Check that the interaction was with a chest
            if (interactable is Chest chest)
            {
                // If the chest opened, show the UI
                if (chest.IsOpen)
                {
                    OpenLootUI(chest);
                    HideInteractableButtonPanel(interactable);
                }
                // If the chest closed, hide the UI
                else
                {
                    CloseLootUI();
                }
            }
        }

        /// <summary>
        /// Handler for leaving the interaction zone
        /// </summary>
        private void OnInteractableExited(IInteractable interactable)
        {
            // If the UI is open and the player moved away from the chest, close it
            if (_isLootUIOpen && _autoCloseOnDistanceExit)
            {
                Debug.Log("[LootUIController] Player left interaction zone, closing loot UI");
                CloseLootUI();
            }
        }

        /// <summary>
        /// Open the loot UI for a specific chest
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

            // Subscribe to chest events
            SubscribeToChestEvents(chest);

            // Bind chest data to the UI
            _chestBinding?.BindToChest(chest);

            // Show the panel
            _lootPanel?.SetActive(true);
            _inventoryPanel?.SetActive(false);
            _interactableButtonPanel?.SetActive(false);
            RefreshInputLockState();
        }

        /// <summary>
        /// Close the loot UI
        /// </summary>
        [Button("Close Loot UI"), DisableInEditorMode]
        public void CloseLootUI()
        {
            if (!_isLootUIOpen)
                return;

            Debug.Log("[LootUIController] Closing loot UI");

            // Close the chest if it was open
            if (_currentChest != null && _currentChest.IsOpen)
            {
                // Use Interact to close the chest correctly
                _currentChest.Interact(_playerInteraction);
            }

            // Unsubscribe from chest events
            if (_currentChest != null)
            {
                UnsubscribeFromChestEvents(_currentChest);
            }

            // Unbind data
            _chestBinding?.BindToChest(null);

            // Hide the panel
            _lootPanel?.SetActive(false);

            _currentChest = null;
            _isLootUIOpen = false;
            RefreshInputLockState();
        }

        /// <summary>
        /// Subscribe to chest events
        /// </summary>
        private void SubscribeToChestEvents(Chest chest)
        {
            if (chest == null)
                return;

            chest.OnChestClosed += OnChestClosedExternally;
        }

        /// <summary>
        /// Unsubscribe from chest events
        /// </summary>
        private void UnsubscribeFromChestEvents(Chest chest)
        {
            if (chest == null)
                return;

            chest.OnChestClosed -= OnChestClosedExternally;
        }

        /// <summary>
        /// Handler for external chest closing (not via the Close button)
        /// </summary>
        private void OnChestClosedExternally(Chest chest)
        {
            Debug.Log("[LootUIController] Chest was closed externally");
            // Close the UI without calling Interact on the chest again

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