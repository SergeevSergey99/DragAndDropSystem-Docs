using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Core
{
    public sealed class SwapSearchContext
    {
        public SwapSearchContext(
            DragContext dragContext,
            DragEntry dragEntry,
            IInventory targetInventory,
            BaseSlot hintedTargetBaseSlot,
            bool preferHint)
        {
            DragContext = dragContext;
            DragEntry = dragEntry;
            TargetInventory = targetInventory;
            HintedTargetBaseSlot = hintedTargetBaseSlot;
            PreferHint = preferHint;
        }

        public DragContext DragContext { get; }
        public DragEntry DragEntry { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot HintedTargetBaseSlot { get; }
        public bool PreferHint { get; }
    }
}
