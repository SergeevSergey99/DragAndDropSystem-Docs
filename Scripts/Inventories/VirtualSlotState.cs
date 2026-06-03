using System.Linq;
using UDND.Core;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Inventories
{
    internal sealed class VirtualSlotState : ISlot
    {
        private ItemStack _stack;

        internal VirtualSlotState(BaseSlot baseSlot)
        {
            BaseSlot = baseSlot;
            _stack = baseSlot?.Stack?.CreateCopy() ?? ItemStack.Empty();
        }

        public BaseSlot BaseSlot { get; }

        // ISlot
        public int Index => BaseSlot?.Index ?? -1;
        public IReadOnlyItemStack Stack => _stack;
        public IInventory Inventory => BaseSlot?.Inventory;
        public SlotRuleValidator SlotRuleValidator => BaseSlot?.SlotRuleValidator;

        public bool IsEmpty => _stack == null || _stack.IsEmpty;
        public int Count => _stack?.Count ?? 0;

        internal bool CanAccept(IItemAdapter itemAdapter, bool uniqueMode)
        {
            if (BaseSlot == null || itemAdapter == null || !BaseSlot.IsInteractable)
                return false;

            if (uniqueMode)
                return _stack.IsEmpty;

            if (_stack.IsEmpty)
                return true;

            return _stack.CanStack(itemAdapter);
        }

        internal void MarkEmpty()
        {
            _stack = ItemStack.Empty();
        }

        internal void Apply(IItemAdapter itemAdapter, int amount)
        {
            if (amount <= 0 || itemAdapter == null)
                return;

            var batch = Enumerable.Repeat(itemAdapter, amount);
            if (_stack.IsEmpty)
                ItemStack.TryCreate(batch, out _stack);
            else
                _stack.TryAddToStack(batch);
        }
    }
}
