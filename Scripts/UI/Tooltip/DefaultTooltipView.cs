using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Tools;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.UI
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

        [Header("Animation")]
        [SerializeField, Tooltip("Use fade-in/out animation")]
        private bool _useFadeAnimation = true;
        [SerializeField, Tooltip("CanvasGroup for animation"), ShowIf(nameof(_useFadeAnimation))]
        private CanvasGroup _canvasGroup;

        [SerializeField, Tooltip("Fade-in speed"), ShowIf(nameof(_useFadeAnimation))]
        private float _fadeInTime = 1f;

        [SerializeField, Tooltip("Fade-out speed"), ShowIf(nameof(_useFadeAnimation))]
        private float _fadeOutTime = 1f;

        // Properties
        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            // Add CanvasGroup if animation is enabled
            if (_useFadeAnimation)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                _canvasGroup.alpha = 0f;
            }

            // Hide initially
            gameObject.SetActive(false);
        }

        public override void Show(IItemAdapter itemAdapter, Action OnCompleted = null)
        {
            if (itemAdapter == null)
            {
                Hide();
                return;
            }

            // Fill content
            SetContent(itemAdapter);

            // Show
            gameObject.SetActive(true);

            // Fade-in animation
            if (_useFadeAnimation && _canvasGroup != null)
            {
                MiniTweenRunner.AutoCreateInstance.AnimateCanvasGroupAlpha(
                    _canvasGroup,
                    _canvasGroup.alpha,
                    1f,
                    _fadeInTime,
                    onComplete: OnCompleted);
            }
            else
            {
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f;

                OnCompleted?.Invoke();
            }
        }

        public override void Hide(Action OnCompleted = null)
        {
            // Fade-out animation
            if (_useFadeAnimation && _canvasGroup != null && gameObject.activeSelf)
            {
                MiniTweenRunner.AutoCreateInstance.AnimateCanvasGroupAlpha(
                    _canvasGroup,
                    _canvasGroup.alpha,
                    0f,
                    _fadeOutTime,
                    onComplete: () =>
                    {
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

        #region Content Population

        /// <summary>
        /// Fill tooltip content
        /// </summary>
        public override void SetContent(IItemAdapter itemAdapter)
        {
            if (itemAdapter == null) return;
            
            // Icon
            SetItemIcon(itemAdapter);
            // Name
            SetItemName(itemAdapter);
            // Description
            SetItemDescription(itemAdapter);
        }

        /// <summary>
        /// Set the item name
        /// </summary>
        private void SetItemName(IItemAdapter itemAdapter)
        {
            _itemNameText.text = itemAdapter?.DisplayName;
        }

        /// <summary>
        /// Set the item description
        /// </summary>
        private void SetItemDescription(IItemAdapter itemAdapter)
        {
            if (_itemDescriptionText == null)
                return;

            string description = GetDescriptionText(itemAdapter);

            if (!string.IsNullOrEmpty(description))
            {
                _itemDescriptionText.text = description;
                _itemDescriptionText.gameObject.SetActive(true);
            }
            else
            {
                _itemDescriptionText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Set the item icon
        /// </summary>
        private void SetItemIcon(IItemAdapter itemAdapter)
        {
            if (_itemIcon == null)
                return;

            if (itemAdapter.Icon != null)
            {
                _itemIcon.sprite = itemAdapter.Icon;
                _itemIcon.gameObject.SetActive(true);
            }
            else
            {
                _itemIcon.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Get description text depending on the item format
        /// </summary>
        private string GetDescriptionText(IItemAdapter itemAdapter)
        {
            if (itemAdapter is IDescribable describableItem)
            {
                return describableItem.Description;
            }
            return string.Empty;
        }

        #endregion
    }
}
