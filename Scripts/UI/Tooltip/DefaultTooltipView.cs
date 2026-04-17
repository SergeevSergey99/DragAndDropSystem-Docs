using System;
using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Tools;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.UI
{
    /// <summary>
    /// Default item tooltip visualization.
    /// Simple card with a name, description, and icon.
    /// Custom visualizations can be created by implementing ITooltipView.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DefaultTooltipView : BaseTooltipView
    {
        [Header("UI Elements")]
        [SerializeField, Tooltip("Item name text")] 
        // Replace to TMP Support
        // private TMPro.TMP_Text _itemNameText;
        private Text _itemNameText;

        [SerializeField, Tooltip("Item description text")]
        // Replace to TMP Support
        // private TMPro.TMP_Text _itemDescriptionText;
        private Text _itemDescriptionText;

        [SerializeField, Tooltip("Item icon")]
        private Image _itemIcon;

        [SerializeField, Tooltip("CanvasGroup for animation")]
        private CanvasGroup _canvasGroup;

        [SerializeField, Tooltip("Fade-in speed")]
        private float _fadeInTime = 1f;

        [SerializeField, Tooltip("Fade-out speed")]
        private float _fadeOutTime = 1f;

        protected void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.alpha = 0f;
        }

        protected override void ShowView(Action OnCompleted)
        {
            gameObject.SetActive(true);

            // Fade-in animation
            if (_canvasGroup != null)
            {
                _canvasGroup.FadeTo(1f, _fadeInTime, OnCompleted);
            }
            else
            {
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f;

                OnCompleted?.Invoke();
            }
        }

        protected override void HideView(Action OnCompleted)
        {
            // Fade-out animation
            if (_canvasGroup != null && gameObject.activeSelf)
            {
                _canvasGroup.FadeTo(0f, _fadeOutTime, () => {
                        gameObject.SetActive(false);
                        OnCompleted?.Invoke();
                    });
            }
            else
            {
                // Hide immediately
                gameObject.SetActive(false);
                OnCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Fill tooltip content
        /// </summary>
        protected override void SetContent(IItemAdapter itemAdapter)
        {
            if (_itemIcon != null)
                _itemIcon.sprite = itemAdapter.Icon;
            
            if (_itemNameText != null)
                _itemNameText.text = itemAdapter.DisplayName;
            
            if (_itemDescriptionText != null)
            {
                if (itemAdapter is IDescribable describableItem)
                {
                    _itemDescriptionText.text = describableItem.Description;
                }
                else
                {
                    _itemDescriptionText.text = string.Empty;
                }
            }
        }
    }
}
