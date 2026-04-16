using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Interaction
{
    /// <summary>
    /// Displays item amount while holding a slot (hold preview).
    /// Subscribes to InputEventRouter.OnHoldPreviewChanged/OnHoldPreviewEnded.
    /// Place it on a Canvas object with Text inside.
    /// </summary>
    public class HoldDragPreviewDisplay : MonoBehaviour
    {
        // Replace to TMP Support
        // [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private Text _countText;
        [SerializeField] private GameObject _container;
        [SerializeField] private Vector2 _offset = new Vector2(0, 40f);

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private BaseSlot _trackedBaseSlot;

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

        private void OnPreviewChanged(BaseSlot baseSlot, int amount, int maxAmount)
        {
            _trackedBaseSlot = baseSlot;

            if (_countText != null)
                _countText.text = amount.ToString();

            if (_container != null)
                _container.SetActive(true);

            UpdatePosition();
        }

        private void OnPreviewEnded()
        {
            _trackedBaseSlot = null;

            if (_container != null)
                _container.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_trackedBaseSlot != null)
                UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_rectTransform == null || _trackedBaseSlot == null)
                return;

            var slotTransform = (_trackedBaseSlot as MonoBehaviour)?.transform as RectTransform;
            if (slotTransform == null)
                return;

            _rectTransform.position = slotTransform.position + (Vector3)_offset;
        }
    }
}
