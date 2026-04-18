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
    public class DefaultTooltipView : FadeTooltipView
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
