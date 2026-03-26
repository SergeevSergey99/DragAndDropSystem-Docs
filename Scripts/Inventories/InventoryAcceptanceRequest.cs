using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Preview-операция проверки, сколько предметов target inventory может принять
    /// в контексте конкретного drag entry.
    /// </summary>
    public sealed class InventoryAcceptanceRequest
    {
        public InventoryAcceptanceRequest(
            IInventory targetInventory,
            IInventoryItem item,
            int desiredCount,
            DragContext context = null,
            DragEntry? sourceEntry = null)
        {
            TargetInventory = targetInventory;
            Item = item;
            DesiredCount = desiredCount;
            Context = context;
            SourceEntry = sourceEntry;
        }

        public IInventory TargetInventory { get; }
        public IInventoryItem Item { get; }
        public int DesiredCount { get; }
        public DragContext Context { get; }
        public DragEntry? SourceEntry { get; }

        public IInventory SourceInventory => SourceEntry.HasValue ? SourceEntry.Value.SourceInventory : null;
        public ISlot SourceSlot => SourceEntry.HasValue ? SourceEntry.Value.SourceSlot : null;

        public DragContext CreateValidationContext(ISlot targetSlot, int previewCount, IInventoryItem previewItem = null)
        {
            var item = previewItem ?? Item;
            var stack = new ItemStack(item, previewCount);
            return new DragContext(stack, SourceSlot, SourceInventory, targetSlot, TargetInventory);
        }
    }
}
