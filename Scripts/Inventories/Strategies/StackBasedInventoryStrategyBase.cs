using System;
using UnityEngine;
using UDND.Core;
using UDND.Tools.Inspector;

namespace UDND.Inventories
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

            return GetMaxStackSize(itemAdapter, _maxStackSize, _allowItemStackOverride);
        }

        protected int DefaultMaxStackSize => _maxStackSize;
        protected bool AllowItemStackOverride => _allowItemStackOverride;
    }
}
