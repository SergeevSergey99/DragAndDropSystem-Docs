using UnityEngine;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Examples.Demo2Loot
{
    /// <summary>
    /// Контроллер игрока для 2D Top-Down вида с ортографической камерой
    /// Управление: WASD - движение в 8 направлениях, Shift - бег
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Tooltip("Скорость ходьбы")]
        private float _walkSpeed = 5f;

        [SerializeField, Tooltip("Скорость бега")]
        private float _runSpeed = 8f;

        [SerializeField, Tooltip("Плавность движения (0 = мгновенно, 1 = максимально плавно)")]
        [Range(0f, 1f)]
        private float _movementSmoothing = 0.05f;

        [Header("Camera")]
        [SerializeField, Tooltip("Камера игрока (должна быть ортографической)")]
        private Camera _playerCamera;

        [SerializeField, Tooltip("Камера следует за игроком")]
        private bool _cameraFollowsPlayer = true;

        [SerializeField, Tooltip("Плавность следования камеры (чем выше значение, тем медленнее камера следует)"), Range(0f, 1f)]
        private float _cameraSmoothing = 0.05f;

        [SerializeField, Tooltip("Смещение камеры от игрока")]
        private Vector3 _cameraOffset = new Vector3(0f, 0f, -10f);

        // State
        private Vector2 _movement;
        private Vector2 _currentVelocity;
        private float _currentAngle;

        // Input lock (когда открыто UI)
        private bool _inputLocked = false;

        public bool InputLocked
        {
            get => _inputLocked;
            set => _inputLocked = value;
        }

        public Camera PlayerCamera => _playerCamera;

        private void Awake()
        {
            // Находим камеру если не назначена
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            // Проверяем что камера ортографическая
            if (_playerCamera != null && !_playerCamera.orthographic)
            {
                Debug.LogWarning("[PlayerController] Camera is not orthographic! Switching to orthographic mode.");
                _playerCamera.orthographic = true;
            }

            // Курсор виден для top-down
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (!_inputLocked)
            {
                // Получаем ввод
                HandleInput();
            }

            // Обновляем позицию камеры
            if (_cameraFollowsPlayer)
            {
                UpdateCameraPosition();
            }
        }

        private void HandleInput()
        {
            // Получаем ввод WASD (8 направлений)
            float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
            float vertical = Input.GetAxisRaw("Vertical");     // W/S

            _movement = new Vector2(horizontal, vertical).normalized;
            
            MovePlayer();
        }

        private void MovePlayer()
        {
            // Определяем скорость (бег или ходьба)
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isRunning ? _runSpeed : _walkSpeed;

            // Целевая скорость
            Vector2 targetVelocity = _movement * currentSpeed;
            
            // Плавное движение
            var smothedVelocity = Vector2.Lerp(_currentVelocity, targetVelocity, _movementSmoothing);
            transform.Translate(smothedVelocity * Time.deltaTime, Space.World);
            _currentVelocity = smothedVelocity;
        }

        Vector3 cameraVelocity = Vector3.zero;
        private void UpdateCameraPosition()
        {
            if (_playerCamera == null)
                return;

            // Целевая позиция камеры
            Vector3 targetPosition = transform.position + _cameraOffset;

            // Плавное следование
            _playerCamera.transform.position = Vector3.SmoothDamp(
                _playerCamera.transform.position,
                targetPosition,
                ref cameraVelocity,
                _cameraSmoothing
            );
        }

        /// <summary>
        /// Блокировать/разблокировать ввод (например, когда открыто UI)
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;

            // Курсор всегда видим в top-down
            Cursor.visible = true;
        }

        /// <summary>
        /// Установить позицию игрока
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);

            // Обновляем позицию камеры мгновенно
            if (_cameraFollowsPlayer && _playerCamera != null)
            {
                _playerCamera.transform.position = transform.position + _cameraOffset;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Отображаем вектор движения
            if (_movement.magnitude > 0.01f)
            {
                Gizmos.color = Color.green;
                Vector3 moveDir = new Vector3(_movement.x, _movement.y, 0f);
                Gizmos.DrawRay(transform.position, moveDir * 2f);
            }

            // Отображаем позицию камеры
            if (_cameraFollowsPlayer && _playerCamera != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + _cameraOffset, 0.5f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Автоматически находим камеру
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }
        }
#endif
    }
}
