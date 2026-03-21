using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal sealed class VirtualSlotState
    {
        private IInventoryItem _item;
        private int _count;

        public VirtualSlotState(ISlot slot)
        {
            Slot = slot;
            if (slot == null || slot.IsEmpty || slot.Stack?.Item == null)
            {
                _item = null;
                _count = 0;
                return;
            }

            _item = slot.Stack.Item;
            _count = slot.Stack.Count;
        }

        public ISlot Slot { get; }
        public bool IsEmpty => _item == null || _count <= 0;

        public bool CanAccept(IInventoryItem item, bool uniqueMode)
        {
            if (Slot == null || item == null || !Slot.IsInteractable)
                return false;

            if (uniqueMode)
                return IsEmpty;

            if (IsEmpty)
                return true;

            return _item.ItemId == item.ItemId;
        }

        public void Apply(IInventoryItem item, int amount)
        {
            if (amount <= 0 || item == null)
                return;

            if (IsEmpty)
            {
                _item = item;
                _count = amount;
            }
            else
            {
                _count += amount;
            }
        }
    }
}
