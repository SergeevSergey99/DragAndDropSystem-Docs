using System.Collections.Generic;
using DragAndDropSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Стандартная визуализация перетаскиваемого предмета
    /// Просто иконка следующая за курсором
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DefaultDragVisual : MonoBehaviour, IDragVisual
    {
        [Header("Components")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMPro.TMP_Text _countText;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField, Min(0)] private int _maxBatchPreviewIcons = 3;
        [SerializeField] private Vector2 _batchPreviewOffset = new Vector2(16f, -12f);
        [SerializeField, Range(0.1f, 1f)] private float _batchPreviewScaleStep = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float _batchPreviewAlpha = 0.75f;

        private RectTransform _rectTransform => transform as RectTransform;
        private readonly List<Image> _batchPreviewImages = new List<Image>();

        public bool IsVisible => gameObject.activeSelf;


        public void Show(IReadOnlyList<DragEntry> entries)
        {
            if (entries == null || entries.Count == 0 || _iconImage == null)
            {
                Hide();
                return;
            }

            var stack = entries[0].Stack;
            if (stack == null || stack.IsEmpty)
            {
                Hide();
                return;
            }

            _iconImage.sprite = stack.Item.Icon;
            _iconImage.color = _normalColor;
            _iconImage.transform.SetAsLastSibling();

            UpdateBatchPreviewIcons(entries);

            if (_showCount && _countText != null)
            {
                // Для batch: показываем общее количество entries, для single - количество в стаке
                if (entries.Count > 1)
                {
                    _countText.gameObject.SetActive(true);
                    _countText.text = entries.Count.ToString();
                }
                else if (stack.Count > 1)
                {
                    _countText.gameObject.SetActive(true);
                    _countText.text = stack.Count.ToString();
                }
                else
                {
                    _countText.gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            HideBatchPreviewIcons();
            gameObject.SetActive(false);
        }

        public void UpdatePosition(Vector3 position)
        {
            if (_rectTransform != null)
            {
                _rectTransform.position = position;
            }
        }

        private void UpdateBatchPreviewIcons(IReadOnlyList<DragEntry> entries)
        {
            int previewCount = entries != null ? Mathf.Min(Mathf.Max(entries.Count - 1, 0), _maxBatchPreviewIcons) : 0;
            EnsureBatchPreviewIcons(previewCount);

            for (int i = 0; i < _batchPreviewImages.Count; i++)
            {
                bool shouldShow = i < previewCount;
                var previewImage = _batchPreviewImages[i];
                previewImage.gameObject.SetActive(shouldShow);

                if (!shouldShow)
                    continue;

                int entryIndex = Mathf.Min(i + 1, entries.Count - 1);
                previewImage.sprite = entries[entryIndex].Stack?.Item?.Icon;

                var color = _normalColor;
                color.a *= _batchPreviewAlpha;
                previewImage.color = color;

                var previewRect = previewImage.rectTransform;
                previewRect.anchorMin = _iconImage.rectTransform.anchorMin;
                previewRect.anchorMax = _iconImage.rectTransform.anchorMax;
                previewRect.pivot = _iconImage.rectTransform.pivot;
                previewRect.sizeDelta = _iconImage.rectTransform.sizeDelta;
                previewRect.anchoredPosition = _batchPreviewOffset * (i + 1);

                float scale = 1f - (_batchPreviewScaleStep * (i + 1));
                previewRect.localScale = new Vector3(scale, scale, 1f);
                previewRect.SetAsFirstSibling();
            }
        }

        private void EnsureBatchPreviewIcons(int previewCount)
        {
            if (_iconImage == null)
                return;

            while (_batchPreviewImages.Count < previewCount)
            {
                var previewObject = new GameObject($"BatchPreview{_batchPreviewImages.Count + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                previewObject.transform.SetParent(_iconImage.transform.parent, false);

                var previewImage = previewObject.GetComponent<Image>();
                previewImage.raycastTarget = false;
                previewImage.preserveAspect = _iconImage.preserveAspect;
                previewImage.type = _iconImage.type;
                previewImage.material = _iconImage.material;

                _batchPreviewImages.Add(previewImage);
            }
        }

        private void HideBatchPreviewIcons()
        {
            for (int i = 0; i < _batchPreviewImages.Count; i++)
            {
                if (_batchPreviewImages[i] != null)
                    _batchPreviewImages[i].gameObject.SetActive(false);
            }
        }
    }
}
