using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;

namespace UDND.UI
{
    /// <summary>
    /// Example of a custom visual with animation and effects
    /// Demonstrates how the default visual can be overridden
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FancyBaseDragVisual : BaseDragVisual
    {
        [Header("Components")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _countParent;
        // Replace to TMP Support
        // [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private Text _countText;
        [SerializeField] private GameObject _rotatingObject;
        [SerializeField] private Image _glowEffect;

        [Header("Animation")]
        [SerializeField] private float _bobSpeed = 5f;
        [SerializeField] private float _bobAmount = 8f;
        [SerializeField] private float _rotationSpeed = 50f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _glowColor = new Color(1f, 1f, 0f, 0.5f);

        private Vector3 _basePosition;
        private float _bobTimer;
        private float _orientationAngle;

        public override void Show(DragEntry entry)
        {
            if (_iconImage == null)
            {
                Hide();
                return;
            }

            var stack = entry.Stack;
            if (stack == null || stack.IsEmpty)
            {
                Hide();
                return;
            }

            _iconImage.sprite = stack.Icon;
            _iconImage.color = _normalColor;
            _orientationAngle = entry.OrientationTopology
                .GetVisualAngleDegrees(entry.Orientation);

            if (_glowEffect != null)
            {
                _glowEffect.color = _glowColor;
            }

            if (_countParent != null)
            {
                if (entry.Stack.Count > 1)
                {
                    _countParent.SetActive(true);
                    if (_countText != null)
                        _countText.text = entry.Stack.Count.ToString();
                }
                else
                {
                    _countParent.SetActive(false);
                }
            }

            _bobTimer = 0f;
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
        }

        public override void UpdatePosition(Vector3 position)
        {
            if (_rectTransform == null)
                return;

            _basePosition = position;

            // Sway animation
            float bobOffset = Mathf.Sin(_bobTimer * _bobSpeed) * _bobAmount;
            _rectTransform.position = _basePosition + Vector3.up * bobOffset;

            // Rotation
            if (_rotatingObject != null)
            {
                _rotatingObject.transform.rotation = Quaternion.Euler(0, 0, _orientationAngle + Mathf.Sin(_bobTimer) * _rotationSpeed);
            }

            // Glow pulse
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