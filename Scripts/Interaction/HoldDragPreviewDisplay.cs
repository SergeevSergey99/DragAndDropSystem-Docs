using DragAndDropSystem.Slots;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Отображает количество предметов при удержании слота (hold preview).
    /// Подписывается на InputEventRouter.OnHoldPreviewChanged/OnHoldPreviewEnded.
    /// Размещайте на Canvas-объекте с TMP_Text внутри.
    /// </summary>
    public class HoldDragPreviewDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private GameObject _container;
        [SerializeField] private Vector2 _offset = new Vector2(0, 40f);

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private ISlot _trackedSlot;

        private void Awake()
        {
            _rectTransform = _container != null
                ? _container.GetComponent<RectTransform>()
                : GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            if (_container != null)
                _container.SetActive(false);
        }

        private void OnEnable()
        {
            InputEventRouter.OnHoldPreviewChanged += OnPreviewChanged;
            InputEventRouter.OnHoldPreviewEnded += OnPreviewEnded;
        }

        private void OnDisable()
        {
            InputEventRouter.OnHoldPreviewChanged -= OnPreviewChanged;
            InputEventRouter.OnHoldPreviewEnded -= OnPreviewEnded;
        }

        private void OnPreviewChanged(ISlot slot, int amount, int maxAmount)
        {
            _trackedSlot = slot;

            if (_countText != null)
                _countText.text = amount.ToString();

            if (_container != null)
                _container.SetActive(true);

            UpdatePosition();
        }

        private void OnPreviewEnded()
        {
            _trackedSlot = null;

            if (_container != null)
                _container.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_trackedSlot != null)
                UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_rectTransform == null || _trackedSlot == null)
                return;

            var slotTransform = (_trackedSlot as MonoBehaviour)?.transform as RectTransform;
            if (slotTransform == null)
                return;

            _rectTransform.position = slotTransform.position + (Vector3)_offset;
        }
    }
}
