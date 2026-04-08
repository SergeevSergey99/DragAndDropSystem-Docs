using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Универсальный слот, работающий через композицию
    /// Не требует наследования для разных типов данных
    /// </summary>
    public class UniversalSlot : BaseSlot
    {
        [Header("Visual Components")]
        [SerializeField] protected Image _iconImage;
        [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private GameObject _countContainer;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Color _emptyColor = new Color(1, 1, 1, 0.3f);
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _highlightColor = Color.yellow;

        [Header("Filter Settings")]
        [SerializeField, Tooltip("Tint color for inactive (filtered) slots")]
        private Color _nonInteractableColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [SerializeField, Tooltip("Optional: CanvasGroup for controlling interactivity")]
        private CanvasGroup _canvasGroup;

        public override void UpdateVisuals()
        {
            SetIconVisibility(IsEmpty == false);
        }
        
        protected virtual void RenderEmpty()
        {
            _iconImage.sprite = null;
            _iconImage.color = _emptyColor;
            _iconImage.enabled = false;
            if (_countContainer != null)
                _countContainer.SetActive(false);
        }
        protected virtual void RenderSetted()
        {
            _iconImage.sprite = Stack.Icon;
            _iconImage.color = _normalColor;
            _iconImage.enabled = true;
        }

        protected virtual void RenderCounter()
        {
            if (_countContainer != null && _showCount)
            {
                bool shouldShowCount = !IsEmpty && Stack.Count > 1;
                _countContainer.SetActive(shouldShowCount);

                if (shouldShowCount && _countText != null)
                {
                    _countText.text = Stack.Count.ToString();
                }
            }
        }

        public override void Highlight(bool highlight)
        {
            if (_iconImage != null && !IsEmpty)
            {
                _iconImage.color = highlight ? _highlightColor : _normalColor;
            }
        }

        public override void SetIconVisibility(bool visible)
        {
            if (visible)
            {
                RenderSetted();
                RenderCounter();
            }
            else
            {
                RenderEmpty();
            }
        }

        /// <summary>
        /// Обновить визуальное состояние в зависимости от интерактивности.
        /// Затемняет слот когда он неактивен (отфильтрован).
        /// </summary>
        protected override void UpdateInteractableVisuals()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = IsInteractable;
                _canvasGroup.blocksRaycasts = IsInteractable;
                _canvasGroup.alpha = IsInteractable ? 1f : 0.5f;
            }

            // Обновляем цвет иконки
            if (_iconImage != null && !IsEmpty)
            {
                _iconImage.color = IsInteractable ? _normalColor : _nonInteractableColor;
            }
        }
    }
}
