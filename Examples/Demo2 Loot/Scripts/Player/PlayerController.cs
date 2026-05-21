using UnityEngine;

namespace UDND.Examples.Loot
{
    /// <summary>
    /// Player controller for a 2D top-down view with an orthographic camera
    /// Controls: WASD for 8-direction movement, Shift for running
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Tooltip("Walk speed")]
        private float _walkSpeed = 5f;

        [SerializeField, Tooltip("Run speed")]
        private float _runSpeed = 8f;

        [SerializeField, Tooltip("Movement smoothing (0 = instant, 1 = maximum smoothing)")]
        [Range(0f, 1f)]
        private float _movementSmoothing = 0.05f;

        [Header("Camera")]
        [SerializeField, Tooltip("Player camera (must be orthographic)")]
        private Camera _playerCamera;

        [SerializeField, Tooltip("Camera follows the player")]
        private bool _cameraFollowsPlayer = true;

        [SerializeField, Tooltip("Camera follow smoothing (higher values make the camera follow more slowly)"), Range(0f, 1f)]
        private float _cameraSmoothing = 0.05f;

        [SerializeField, Tooltip("Camera offset from the player")]
        private Vector3 _cameraOffset = new Vector3(0f, 0f, -10f);

        // State
        private Vector2 _movement;
        private Vector2 _currentVelocity;
        private float _currentAngle;

        // Input lock (when the UI is open)
        private bool _inputLocked = false;

        public bool InputLocked
        {
            get => _inputLocked;
            set => _inputLocked = value;
        }

        public Camera PlayerCamera => _playerCamera;

        private void Awake()
        {
            // Find the camera if it is not assigned
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            // Ensure the camera is orthographic
            if (_playerCamera != null && !_playerCamera.orthographic)
            {
                Debug.LogWarning("[PlayerController] Camera is not orthographic! Switching to orthographic mode.");
                _playerCamera.orthographic = true;
            }

            // Cursor is visible in top-down mode
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (!_inputLocked)
            {
                // Read input
                HandleInput();
            }

            // Update the camera position
            if (_cameraFollowsPlayer)
            {
                UpdateCameraPosition();
            }
        }

        private void HandleInput()
        {
            // Read WASD input (8 directions)
            float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
            float vertical = Input.GetAxisRaw("Vertical");     // W/S

            _movement = new Vector2(horizontal, vertical).normalized;
            
            MovePlayer();
        }

        private void MovePlayer()
        {
            // Determine speed (running or walking)
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isRunning ? _runSpeed : _walkSpeed;

            // Target speed
            Vector2 targetVelocity = _movement * currentSpeed;
            
            // Smooth movement
            var smothedVelocity = Vector2.Lerp(_currentVelocity, targetVelocity, _movementSmoothing);
            transform.Translate(smothedVelocity * Time.deltaTime, Space.World);
            _currentVelocity = smothedVelocity;
        }

        Vector3 cameraVelocity = Vector3.zero;
        private void UpdateCameraPosition()
        {
            if (_playerCamera == null)
                return;

            // Target camera position
            Vector3 targetPosition = transform.position + _cameraOffset;

            // Smooth follow
            _playerCamera.transform.position = Vector3.SmoothDamp(
                _playerCamera.transform.position,
                targetPosition,
                ref cameraVelocity,
                _cameraSmoothing
            );
        }

        /// <summary>
        /// Lock/unlock input (for example, when the UI is open)
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;

            // Cursor is always visible in top-down mode
            Cursor.visible = true;
        }

        /// <summary>
        /// Set player position
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);

            // Update camera position immediately
            if (_cameraFollowsPlayer && _playerCamera != null)
            {
                _playerCamera.transform.position = transform.position + _cameraOffset;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw movement vector
            if (_movement.magnitude > 0.01f)
            {
                Gizmos.color = Color.green;
                Vector3 moveDir = new Vector3(_movement.x, _movement.y, 0f);
                Gizmos.DrawRay(transform.position, moveDir * 2f);
            }

            // Draw camera position
            if (_cameraFollowsPlayer && _playerCamera != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + _cameraOffset, 0.5f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Automatically find the camera
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }
        }
#endif
    }
}