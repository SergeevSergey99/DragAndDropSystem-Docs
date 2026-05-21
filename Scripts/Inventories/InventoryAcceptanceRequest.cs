using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Preview request used to check how many items the target inventory can accept
    /// in the context of a specific drag entry.
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
        public BaseSlot SourceBaseSlot => SourceEntry.HasValue ? SourceEntry.Value.SourceBaseSlot : null;

        public ItemStack CreatePreviewStack(int previewCount, IItemAdapter previewItemAdapter = null)
        {
            return TransferItemConversionUtility.TryCreatePreviewStack(this, previewCount, previewItemAdapter, out var previewStack)
                ? previewStack
                : null;
        }

        public DragContext CreateValidationContext(BaseSlot targetBaseSlot, int previewCount, IItemAdapter previewItemAdapter = null)
        {
            var stack = CreatePreviewStack(previewCount, previewItemAdapter);
            if (stack == null)
                return null;

            return new DragContext(stack, SourceBaseSlot, SourceInventory, targetBaseSlot, TargetInventory);
        }
    }
}
