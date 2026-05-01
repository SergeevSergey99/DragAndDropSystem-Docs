using System;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public abstract class StackBasedInventoryStrategyBase : InventoryStrategyBase
    {
        [SerializeField, Tooltip("Maximum stack size. 0 or less = unlimited.")]
        private int _maxStackSize;

        [SerializeField, Tooltip("Allow items to override the stack limit via IStackSizeLimitable.")]
        [ShowIf(nameof(ShowItemStackOverride))]
        private bool _allowItemStackOverride;

        private bool ShowItemStackOverride => _maxStackSize > 0;

        public override void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _maxStackSize = maxStackSize;
            _allowItemStackOverride = allowItemOverride;
        }

        public override int GetMaxStackSizeForItem(IItemAdapter itemAdapter)
        {
            if (itemAdapter == null)
                return 0;

            if (!Footprint.Resolve(itemAdapter).IsSingleCell)
                return 1;

            if (_allowItemStackOverride && itemAdapter is IStackSizeLimitable limitable)
                return Mathf.Max(1, limitable.MaxStackSize);

            return _maxStackSize > 0 ? _maxStackSize : int.MaxValue;
        }

        protected int DefaultMaxStackSize => _maxStackSize;
        protected bool AllowItemStackOverride => _allowItemStackOverride;
    }
}
