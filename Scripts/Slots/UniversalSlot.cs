using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Универсальный слот с визуальным слоем на основе Image + TMP_Text.
    /// Расширяется через переопределение виртуальных Render-методов
    /// и хука <see cref="OnVisualsUpdated"/>.
    /// </summary>
    public class UniversalSlot : BaseSlot
    {
        [Header("Visual Components")]
        [SerializeField] protected Image _iconImage;
        [SerializeField] private TMPro.TMP_Text _countText;
        [SerializeField] private GameObject _countContainer;

        [Header("Settings")]
        [SerializeField] private bool _showCount = true;
        [SerializeField] private Color _emptyColor       = new Color(1, 1, 1, 0.3f);
        [SerializeField] private Color _normalColor      = Color.white;
        [SerializeField] private Color _highlightColor   = Color.yellow;

        [Header("Filter Settings")]
        [SerializeField, Tooltip("Tint color for inactive (filtered) slots")]
        private Color _nonInteractableColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [SerializeField, Tooltip("Optional: CanvasGroup for controlling interactivity")]
        private CanvasGroup _canvasGroup;

        protected override void RenderFilled()
        {
            _iconImage.sprite  = Stack.Icon;
            _iconImage.color   = ResolveIconColor();
            _iconImage.enabled = true;
            RenderCounter();
        }

        protected override void RenderEmpty()
        {
            _iconImage.sprite  = null;
            _iconImage.color   = _emptyColor;
            _iconImage.enabled = false;
            if (_countContainer != null)
                _countContainer.SetActive(false);
        }
        
        /// <summary>Обновляет счётчик стака. Показывается только если предметов больше одного.</summary>
        protected virtual void RenderCounter()
        {
            if (_countContainer == null || !_showCount) return;

            bool shouldShow = !IsEmpty && Stack.Count > 1;
            _countContainer.SetActive(shouldShow);

            if (shouldShow && _countText != null)
                _countText.text = Stack.Count.ToString();
        }
        
        public override void Highlight(bool highlight)
        {
            _isHighlighted = highlight;
            if (_iconImage != null)
                _iconImage.color = ResolveIconColor();
        }

        /// <summary>
        /// Обновляет визуал при смене интерактивности.
        /// CanvasGroup блокирует raycast; цвет иконки идёт через ResolveIconColor,
        /// чтобы не конфликтовать с Highlight и другими состояниями.
        /// </summary>
        protected override void UpdateInteractableVisuals()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable  = IsInteractable;
                _canvasGroup.blocksRaycasts = IsInteractable;
                _canvasGroup.alpha         = IsInteractable ? 1f : 0.5f;
            }

            if (_iconImage != null)
                _iconImage.color = ResolveIconColor();
        }
        
        /// <summary>
        /// Возвращает актуальный цвет иконки с учётом всех активных состояний.
        /// Приоритет: NonInteractable → Highlighted → Normal / Empty.
        /// </summary>
        private Color ResolveIconColor()
        {
            if (!IsInteractable) return _nonInteractableColor;
            if (_isHighlighted)  return _highlightColor;
            return IsEmpty ? _emptyColor : _normalColor;
        }
    }
}
