using UnityEngine;
using UnityEngine.EventSystems;
using UDND.Core;
using UDND.Interaction;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools;

namespace UDND.UI
{
    /// <summary>
    /// Inventory-bound drop area.
    /// Allows dropping items anywhere inside the inventory, not just onto a specific slot.
    /// Delegates area drops to the JIT transfer pipeline without preselecting a slot.
    /// </summary>
    public class InventoryDropArea : DropAreaBase
    {
        [SerializeField, Tooltip("Inventory bound to this area")]
        private BaseInventory _inventory;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Highlight the area on hover (if it can accept the item)")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        [Header("Drop Policy Override")]
        [SerializeField, Tooltip("Optional policy override for this drop zone. If disabled, the inventory policy is used.")]
        private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        public IInventory Inventory => _inventory;

#if UNITY_EDITOR
        // ══════════════════════════════════════════════════════════
        //  Lifecycle overrides
        // ══════════════════════════════════════════════════════════

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_inventory == null)
                _inventory = GetComponentInParent<BaseInventory>();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  Gamepad focus
        // ══════════════════════════════════════════════════════════

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            InputEventRouter.AutoCreateInstance.RouteDropAreaFocusEnter(this, FocusSource.Gamepad);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            InputEventRouter.Instance.RouteDropAreaFocusExit(this, FocusSource.Gamepad);
        }

        // ══════════════════════════════════════════════════════════
        //  DropAreaBase overrides
        // ══════════════════════════════════════════════════════════

        internal override bool TryActivateAsFocusedTarget()
        {
            if (DragManager == null || !DragManager.IsDragging || _inventory == null)
                return false;

            var context = DragManager.CurrentContext;
            if (context == null || !CreateDropProcessor().CanAcceptDrop(
                    context.WithTarget(null, _inventory)))
                return false;

            DragManager.PushDropTarget(this);
            Extensions.DragAndDropLog(
                $"<color=cyan>[InventoryDropArea] Entered, inventory={_inventory.name}</color>");
            return true;
        }

        protected override void OnHighlightChanged(bool highlighted, bool canAccept)
        {
            if (_areaHighlight == null)
                return;
            _areaHighlight.color = highlighted ? _highlightColor : _normalColor;
        }

        // ══════════════════════════════════════════════════════════
        //  IDropTarget overrides (delegated processing)
        // ══════════════════════════════════════════════════════════

        public override BaseSlot GetTargetSlot() => null;

        public override IDropProcessor GetDropProcessor()
        {
            return CreateDropProcessor();
        }

        // ══════════════════════════════════════════════════════════
        //  Domain logic
        // ══════════════════════════════════════════════════════════

        private InventoryDropProcessor CreateDropProcessor()
        {
            var boundOverride = _dropPolicyOverride != null ? _dropPolicyOverride.TryBuild() : (DropRequestPolicy?)null;
            System.Func<InventorySwapContext, bool> swapAttempting = DragManager != null
                ? DragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = DragManager != null
                ? DragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropProcessor(
                _inventory,
                DragManager?.GlobalRules,
                boundOverride,
                swapAttempting,
                swapCompleted);
        }
    }
}
