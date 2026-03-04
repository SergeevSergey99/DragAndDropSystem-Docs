using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Компонент для области дропа инвентаря
    /// Позволяет дропать предметы в любое место инвентаря, а не только в конкретный слот
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryDropArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropTarget
    {
        private DragAndDropManager _dragManager => DragAndDropManager.Instance;

        [SerializeField, Tooltip("Инвентарь, к которому привязана эта область")]
        private UniversalInventory _inventory;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Подсвечивать область при наведении (если может принять предмет)")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        [Header("Drop Policy Override")]
        [SerializeField, Tooltip("Опциональный override policy для этой зоны дропа. Если выключен - используется policy инвентаря.")]
        private DropPolicySettings _dropPolicyOverride = new DropPolicySettings();

        private ISlot _foundSlot;
        private bool _isHighlighted;
        private UnityEngine.UI.Graphic _raycastGraphic;

        private void OnValidate()
        {
            // Автоматически находим инвентарь на этом объекте или родителе
            if (_inventory == null)
            {
                _inventory = GetComponentInParent<UniversalInventory>();
            }

            if (_raycastGraphic == null)
            {
                _raycastGraphic = GetComponent<UnityEngine.UI.Graphic>();
            }
        }

        private void Awake()
        {
            if (_raycastGraphic == null)
            {
                _raycastGraphic = GetComponent<UnityEngine.UI.Graphic>();
            }
        }

        private void LateUpdate()
        {
            if (_raycastGraphic == null)
                return;

            bool shouldReceiveRaycast = _dragManager != null && _dragManager.IsDragging;
            if (_raycastGraphic.raycastTarget != shouldReceiveRaycast)
            {
                _raycastGraphic.raycastTarget = shouldReceiveRaycast;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_dragManager == null || !_dragManager.IsDragging || _inventory == null)
                return;

            var context = _dragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return;

            var stack = context.Entries[0].Stack;
            if (stack == null || stack.Item == null)
                return;

            // Проверяем может ли инвентарь принять этот предмет
            bool canAccept = _inventory.CanAcceptItem(stack.Item, stack.Count, out ISlot suggestedSlot);

            if (!canAccept)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryDropArea] Cannot accept item in {_inventory.name}</color>");
                return;
            }

            // Сохраняем найденный слот (может быть null для создания нового)
            _foundSlot = suggestedSlot;

            // Добавляем себя в стек целей
            _dragManager.PushDropTarget(this);

            Extentions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Entered, slot={_foundSlot?.Index.ToString() ?? "AREA"}, inventory={_inventory.name}</color>");
        }

        /// <summary>
        /// Подсветить/снять подсветку области
        /// </summary>
        private void HighlightArea(bool highlight)
        {
            if (_areaHighlight == null)
                return;

            _isHighlighted = highlight;
            _areaHighlight.color = highlight ? _highlightColor : _normalColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragManager == null || !_dragManager.IsDragging)
                return;

            // Удаляем себя из стека целей
            _dragManager.PopDropTarget(this);

            _foundSlot = null;

            Extentions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Exited</color>");
        }

        private void OnDisable()
        {
            if (!DragAndDropManager.IsInstanceExist) return;
            // Удаляем себя из стека при отключении
            if (_dragManager != null && _dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(this);
            }
        }

        // ===== IDropTarget Implementation =====

        public ISlot GetTargetSlot() => _foundSlot;

        public IDropProcessor GetDropHandler()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = _dragManager != null
                ? _dragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = _dragManager != null
                ? _dragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropProcessor(
                _foundSlot,
                _inventory,
                _dragManager?.GlobalRules,
                _dragManager?.TransferService,
                _dropPolicyOverride?.BuildOrNull(),
                swapAttempting,
                swapCompleted);
        }

        public void OnBecomeActiveTarget()
        {
            // Подсвечиваем область когда становимся активной целью
            HighlightArea(true);
        }

        public void OnBecomeInactiveTarget()
        {
            // Снимаем подсветку когда перестаём быть активной целью
            HighlightArea(false);
        }
    }
}
