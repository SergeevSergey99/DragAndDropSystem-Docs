using System;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public abstract class InventoryStrategySettingsBase
    {
        [SerializeField, LabelText("Drag Amount"), Tooltip("How many items to take when dragging from a stack.")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private DragAmount _dragAmount = DragAmount.All;

        [SerializeField, Tooltip("Item amount for Custom drag.")]
        [ShowIf(nameof(ShowCustomDragAmount))]
        private int _customDragAmount = 1;

        private bool ShowCustomDragAmount => ShowDragAmountSettings && _dragAmount == DragAmount.Custom;

        protected virtual bool ShowDragAmountSettings => true;

        public abstract IInventoryStrategy CreateRuntimeStrategy(UniversalInventory inventory);

        public virtual void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
        }

        public virtual void SetDragSettings(DragAmount dragAmount, int customDragAmount)
        {
            _dragAmount = dragAmount;
            _customDragAmount = customDragAmount;
        }

        public virtual int GetMaxStackSizeForItem(IItemAdapter itemAdapter)
        {
            return itemAdapter == null ? 0 : int.MaxValue;
        }

        public virtual UniversalInventory.ItemBehaviorType GetLegacyBehaviorType()
        {
            return UniversalInventory.ItemBehaviorType.Stackable;
        }

        public virtual int ResolveDragAmount(IDragPolicy dragPolicy, int stackCount, DragAmount? overrideAmount = null, int? overrideCustom = null)
        {
            if (dragPolicy == null || stackCount <= 0)
                return 0;

            DragAmount amount = overrideAmount ?? _dragAmount;
            int customAmount = overrideAmount.HasValue ? (overrideCustom ?? 0) : _customDragAmount;
            return dragPolicy.ResolveDragAmount(stackCount, amount, customAmount);
        }

        internal string CaptureConfigurationJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
