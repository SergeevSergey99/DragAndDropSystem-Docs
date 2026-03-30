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
            IItemAdapter itemAdapter,
            int desiredCount,
            DragContext context = null,
            DragEntry? sourceEntry = null)
        {
            TargetInventory = targetInventory;
            ItemAdapter = itemAdapter;
            DesiredCount = desiredCount;
            Context = context;
            SourceEntry = sourceEntry;
        }

        public IInventory TargetInventory { get; }
        public IItemAdapter ItemAdapter { get; }
        public int DesiredCount { get; }
        public DragContext Context { get; }
        public DragEntry? SourceEntry { get; }

        public IInventory SourceInventory => SourceEntry.HasValue ? SourceEntry.Value.SourceInventory : null;
        public ISlot SourceSlot => SourceEntry.HasValue ? SourceEntry.Value.SourceSlot : null;

        public DragContext CreateValidationContext(ISlot targetSlot, int previewCount, IItemAdapter previewItemAdapter = null)
        {
            var item = previewItemAdapter ?? ItemAdapter;
            var stack = new ItemStack(item, previewCount);
            return new DragContext(stack, SourceSlot, SourceInventory, targetSlot, TargetInventory);
        }
    }
}
