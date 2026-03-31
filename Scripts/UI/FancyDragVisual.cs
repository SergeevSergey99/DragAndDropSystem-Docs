using System.Collections.Generic;
using DragAndDropSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Пример кастомного визуала с анимацией и эффектами
    /// Показывает как можно переопределить стандартный визуал
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FancyDragVisual : MonoBehaviour, IDragVisual
    {
        [Header("Components")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private Image _glowEffect;

        [Header("Animation")]
        [SerializeField] private float _bobSpeed = 2f;
        [SerializeField] private float _bobAmount = 5f;
        [SerializeField] private float _rotationSpeed = 50f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _glowColor = new Color(1f, 1f, 0f, 0.5f);

        private RectTransform _rectTransform;
        private Vector3 _basePosition;
        private float _bobTimer;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Hide();
        }

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

            if (_glowEffect != null)
            {
                _glowEffect.color = _glowColor;
            }

            if (_countText != null)
            {
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

            _isVisible = true;
            _bobTimer = 0f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _isVisible = false;
            gameObject.SetActive(false);
        }

        public void UpdatePosition(Vector3 position)
        {
            if (_rectTransform == null)
                return;

            _basePosition = position;

            // Анимация покачивания
            float bobOffset = Mathf.Sin(_bobTimer * _bobSpeed) * _bobAmount;
            _rectTransform.position = _basePosition + Vector3.up * bobOffset;

            // Вращение
            if (_iconImage != null)
            {
                _iconImage.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(_bobTimer) * _rotationSpeed);
            }

            // Пульсация свечения
            if (_glowEffect != null)
            {
                float glowAlpha = 0.3f + Mathf.Sin(_bobTimer * 3f) * 0.2f;
                var color = _glowColor;
                color.a = glowAlpha;
                _glowEffect.color = color;
            }

            _bobTimer += Time.deltaTime;
        }
    }
}
