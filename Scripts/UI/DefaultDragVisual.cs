using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UDND.Core;

namespace UDND.UI
{
    /// <summary>
    /// Default dragged-item visualization
    /// A simple icon that follows the cursor
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DefaultDragVisual : IDragVisual
    {
        [Header("Components")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _countParent;
        
        // Replace to TMP Support
        // [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private Text _countText;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Color _normalColor = Color.white;

        public override void Show(IReadOnlyList<DragEntry> entries)
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
            _iconImage.rectTransform.localEulerAngles = ToEulerAngles(entries[0]);

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

        public override void Hide()
        {
            gameObject.SetActive(false);
        }

        private static Vector3 ToEulerAngles(DragEntry entry)
            => new Vector3(
                0f,
                0f,
                entry.OrientationTopology.GetVisualAngleDegrees(entry.Orientation));
    }
}
