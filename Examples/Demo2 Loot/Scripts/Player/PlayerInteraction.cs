using System;
using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// System for player interaction with world objects.
    /// Does NOT know about the UI; it only determines what can be interacted with and raises events.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField, Tooltip("Search radius for interactable objects")]
        private float _interactionRadius = 2f;

        [SerializeField, Tooltip("Interactable objects layer")]
        private LayerMask _interactableLayer = -1; // All layers by default

        [SerializeField, Tooltip("Interaction key")]
        private KeyCode _interactKey = KeyCode.E;

        // Components
        private PlayerController _playerController;

        // State
        private IInteractable _currentInteractable;
        private Collider2D[] _overlapResults = new Collider2D[10]; // Buffer for overlap results
        ContactFilter2D _contactFilter;
        // Events (the UI subscribes to them)
        /// <summary>
        /// Player entered an object's interaction zone
        /// </summary>
        public event Action<IInteractable> OnInteractableEntered;

        /// <summary>
        /// Player left the interaction zone
        /// </summary>
        public event Action<IInteractable> OnInteractableExited;

        /// <summary>
        /// Player interacted with an object
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
            // Search for interactable objects nearby
            FindNearestInteractable();

            // Process input
            HandleInteractionInput();
        }

        private void FindNearestInteractable()
        {
            // If input is locked (for example, UI is open), do not search
            if (_playerController != null && _playerController.InputLocked)
            {
                return;
            }

            // Search for colliders in range
            int count = Physics2D.OverlapCircle(
                transform.position,
                _interactionRadius,
                _contactFilter,
                _overlapResults
            );
            
            IInteractable nearest = null;
            float nearestDistance = float.MaxValue;

            // Find the nearest interactable object
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

            // Update the current interaction object
            UpdateCurrentInteractable(nearest);
        }

        private void UpdateCurrentInteractable(IInteractable newInteractable)
        {
            // If the object did not change, do nothing
            if (_currentInteractable == newInteractable)
                return;
            
            if (newInteractable != null && !newInteractable.CanInteract(this)) return;
            
            // If there was a previous object, raise the exit event
            if (_currentInteractable != null)
            {
                OnInteractableExited?.Invoke(_currentInteractable);
            }

            // Set the new object
            _currentInteractable = newInteractable;
            Debug.Log($"Current interactable updated to: {_currentInteractable}");

            // If there is a new object, raise the enter event
            if (_currentInteractable != null)
            {
                
                OnInteractableEntered?.Invoke(_currentInteractable);
            }
        }

        private void HandleInteractionInput()
        {
            // If there is no object to interact with, exit
            if (_currentInteractable == null)
                return;

            // If the interaction key is pressed
            if (Input.GetKeyDown(_interactKey))
            {
                // Perform interaction on the object
                _currentInteractable.Interact(this);

                // Raise the event (the UI will subscribe and show/hide the window)
                OnInteracted?.Invoke(_currentInteractable);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction radius
            Gizmos.color = _currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);

            // If there is a current object, draw a line to it
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
