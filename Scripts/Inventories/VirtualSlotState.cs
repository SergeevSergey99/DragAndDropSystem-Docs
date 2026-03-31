using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal sealed class VirtualSlotState
    {
        private IItemAdapter _itemAdapter;
        private int _count;

        public VirtualSlotState(ISlot slot)
        {
            Slot = slot;
            if (slot == null || slot.IsEmpty || slot.Stack?.PrimaryAdapter == null)
            {
                _itemAdapter = null;
                _count = 0;
                return;
            }

            _itemAdapter = slot.Stack.PrimaryAdapter;
            _count = slot.Stack.Count;
        }

        public ISlot Slot { get; }
        public bool IsEmpty => _itemAdapter == null || _count <= 0;
        public IItemAdapter ItemAdapter => _itemAdapter;
        public int Count => _count;

        public bool CanAccept(IItemAdapter itemAdapter, bool uniqueMode)
        {
            if (Slot == null || itemAdapter == null || !Slot.IsInteractable)
                return false;

            if (uniqueMode)
                return IsEmpty;

            if (IsEmpty)
                return true;

            return _itemAdapter.ItemId == itemAdapter.ItemId
                && _itemAdapter.GetType() == itemAdapter.GetType();
        }

        public void Apply(IItemAdapter itemAdapter, int amount)
        {
            if (amount <= 0 || itemAdapter == null)
                return;

            if (IsEmpty)
            {
                _itemAdapter = itemAdapter;
                _count = amount;
            }
            else
            {
                _count += amount;
            }
        }
    }
}
