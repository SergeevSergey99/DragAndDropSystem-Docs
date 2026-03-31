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
        [SerializeField] private GameObject _countParent;
        [SerializeField] private TMPro.TMP_Text _countText;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Color _normalColor = Color.white;

        private RectTransform _rectTransform => transform as RectTransform;

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

            _iconImage.sprite = stack.Icon;
            _iconImage.color = _normalColor;

            if (_showCount && _countText != null)
            {
                if (stack.Count > 1)
                {
                    _countParent.gameObject.SetActive(true);
                    _countText.text = stack.Count.ToString();
                }
                else
                {
                    _countParent.gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void UpdatePosition(Vector3 position)
        {
            if (_rectTransform != null)
            {
                _rectTransform.position = position;
            }
        }
    }
}
