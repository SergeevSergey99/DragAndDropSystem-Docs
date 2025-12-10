using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.World3D
{
    /// <summary>
    /// Компонент для предметов, выброшенных в 3D мир
    /// Хранит ссылку на оригинальный IInventoryItem
    /// Опционально может быть подобран обратно в инвентарь
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        [Header("Item Data")]
        [SerializeField, Tooltip("Данные предмета")]
        private ScriptableObject _itemDataReference; // Для отображения в инспекторе

        [SerializeField, Tooltip("Количество предметов")]
        private int _count = 1;

        private IInventoryItem _itemData;

        [Header("Pickup Settings")]
        [SerializeField, Tooltip("Можно ли подобрать предмет")]
        private bool _canBePickedUp = true;

        [SerializeField, Tooltip("Радиус взаимодействия для подбора")]
        private float _pickupRadius = 2f;

        [SerializeField, Tooltip("Слой игрока для автоматического подбора (опционально)")]
        private LayerMask _playerLayer;

        [Header("Visual")]
        [SerializeField, Tooltip("Подсветка при возможности подбора")]
        private Renderer _highlightRenderer;

        [SerializeField] private Color _highlightColor = Color.yellow;
        private Color _originalColor;
        private MaterialPropertyBlock _propertyBlock;

        public IInventoryItem ItemData => _itemData;
        public int Count => _count;
        public bool CanBePickedUp => _canBePickedUp;

        private void Awake()
        {
            // Если есть рендерер для подсветки - сохраняем оригинальный цвет
            if (_highlightRenderer != null)
            {
                _propertyBlock = new MaterialPropertyBlock();
                _highlightRenderer.GetPropertyBlock(_propertyBlock);
                if (_propertyBlock.isEmpty)
                {
                    _originalColor = _highlightRenderer.material.color;
                }
                else
                {
                    _originalColor = _propertyBlock.GetColor("_Color");
                }
            }
        }

        /// <summary>
        /// Инициализировать предмет с данными
        /// </summary>
        public void Initialize(IInventoryItem itemData, int count = 1)
        {
            _itemData = itemData;
            _count = Mathf.Max(1, count);

            // Сохраняем ссылку для инспектора (если это ScriptableObject)
            if (itemData is ScriptableObject so)
            {
                _itemDataReference = so;
            }
        }

        /// <summary>
        /// Попытаться подобрать предмет в указанный инвентарь
        /// Возвращает true если предмет был успешно добавлен
        /// </summary>
        public bool TryPickup(DragAndDropSystem.Inventories.IInventory targetInventory)
        {
            if (!_canBePickedUp || _itemData == null || targetInventory == null)
                return false;

            // Пытаемся добавить в инвентарь
            bool success = targetInventory.TryAddItem(_itemData, _count);

            if (success)
            {
                // Уничтожаем объект если успешно добавлен
                Destroy(gameObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Подсветить предмет (например, когда игрок рядом)
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            if (_highlightRenderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            _highlightRenderer.GetPropertyBlock(_propertyBlock);

            if (highlight)
            {
                _propertyBlock.SetColor("_Color", _highlightColor);
            }
            else
            {
                _propertyBlock.SetColor("_Color", _originalColor);
            }

            _highlightRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDrawGizmosSelected()
        {
            // Отображаем радиус подбора в редакторе
            if (_canBePickedUp)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, _pickupRadius);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Синхронизация _itemDataReference обратно в _itemData при изменении в инспекторе
            if (_itemDataReference is IInventoryItem item)
            {
                _itemData = item;
            }
        }
#endif
    }
}
