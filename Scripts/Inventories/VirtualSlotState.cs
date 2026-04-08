using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal sealed class VirtualSlotState
    {
        private IItemAdapter _itemAdapter;
        private int _count;

        public VirtualSlotState(BaseSlot baseSlot)
        {
            BaseSlot = baseSlot;
            if (baseSlot == null || baseSlot.IsEmpty || baseSlot.Stack?.PrimaryAdapter == null)
            {
                _itemAdapter = null;
                _count = 0;
                return;
            }

            _itemAdapter = baseSlot.Stack.PrimaryAdapter;
            _count = baseSlot.Stack.Count;
        }

        public BaseSlot BaseSlot { get; }
        public bool IsEmpty => _itemAdapter == null || _count <= 0;
        public IItemAdapter ItemAdapter => _itemAdapter;
        public int Count => _count;

        public bool CanAccept(IItemAdapter itemAdapter, bool uniqueMode)
        {
            if (BaseSlot == null || itemAdapter == null || !BaseSlot.IsInteractable)
                return false;

            if (uniqueMode)
                return IsEmpty;

            if (IsEmpty)
                return true;

            return _itemAdapter.ItemId == itemAdapter.ItemId
                && _itemAdapter.GetType() == itemAdapter.GetType();
        }

        public void MarkEmpty()
        {
            _itemAdapter = null;
            _count = 0;
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
