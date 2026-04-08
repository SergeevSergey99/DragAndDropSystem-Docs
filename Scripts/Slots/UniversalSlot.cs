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

        //[FoldoutGroup("Slot Rules", expanded: false)]
        [InfoBox("Правила фильтрации для этого конкретного слота. Оставьте пустым для слота без ограничений.")]
        [SerializeField, HideLabel]
        private SlotRuleValidator _slotRuleValidator = new SlotRuleValidator();
        
        private ItemStack _stack = ItemStack.Empty();
        private int _index;
        private IInventory _inventory;


        public override ItemStack Stack => _stack;
        public override int Index => _index;
        public override bool IsEmpty => _stack == null || _stack.IsEmpty;
        public override IInventory Inventory => _inventory;
        public override SlotRuleValidator SlotRuleValidator => _slotRuleValidator;

        private void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
            _slotRuleValidator?.OnValidate();
        }

        public override void Initialize(int index, IInventory inventory)
        {
            _inventory = inventory;
            _index = index;
            UpdateVisuals();
        }

        public override void SetStack(ItemStack stack)
        {
            _stack = stack ?? ItemStack.Empty();
            UpdateVisuals();
        }

        public override void ReplaceItem(IItemAdapter newItemAdapter)
        {
            if (_stack == null || _stack.IsEmpty)
            {
                // Если слот пуст, создаем новый стек с количеством 1
                if (!ItemStack.TryCreate(new[] { newItemAdapter }, out _stack))
                    return;
            }
            else
            {
                if (_stack.Count > 1)
                {
                    Extensions.DragAndDropLog("<color=red>[UniversalSlot] ReplaceItem cannot operate on stacks with multiple concrete adapters. Rebuild the slot stack explicitly.</color>");
                    return;
                }

                _stack.ReplaceItem(newItemAdapter);
            }

            UpdateVisuals();
        }

        public override void Clear()
        {
            _stack = ItemStack.Empty();
            UpdateVisuals();
        }

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
            _iconImage.sprite = _stack.Icon;
            _iconImage.color = _normalColor;
            _iconImage.enabled = true;
        }

        protected virtual void RenderCounter()
        {
            if (_countContainer != null && _showCount)
            {
                bool shouldShowCount = !IsEmpty && _stack.Count > 1;
                _countContainer.SetActive(shouldShowCount);

                if (shouldShowCount && _countText != null)
                {
                    _countText.text = _stack.Count.ToString();
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

        /// <summary>
        /// Временно скрыть/показать визуал слота (для анимаций автопереноса)
        /// </summary>
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
