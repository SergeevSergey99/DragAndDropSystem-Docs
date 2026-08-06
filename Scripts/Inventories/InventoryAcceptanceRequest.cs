using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Preview request used to check how many items the target inventory can accept
    /// in the context of a specific drag entry.
    /// <para>
    /// <see cref="ItemAdapter"/> is already expressed in the target domain, while
    /// <see cref="SourceEntry"/> still carries the source-domain instances. Placement questions
    /// (footprint, occupancy, capacity) use the target view; drop rules are fed the source view
    /// and converted once by the rule pipeline.
    /// </para>
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

        /// <summary>Conversions already resolved by this drag, if the request belongs to one.</summary>
        internal TransferConversionSession ConversionSession => Context?.ConversionSession;

        public ItemStack CreatePreviewStack(int previewCount, IItemAdapter previewItemAdapter = null)
        {
            return TransferItemConversionUtility.TryCreatePreviewStack(this, previewCount, previewItemAdapter, out var previewStack)
                ? previewStack
                : null;
        }

        /// <summary>
        /// Context for drop-rule validation.
        /// <para>
        /// When the request has concrete source instances the context is built from them, so the
        /// rule pipeline performs the single conversion to the target domain and the target's
        /// rules see the item as it would exist after the drop. A request without source instances
        /// is already target-domain and is passed through unchanged.
        /// </para>
        /// </summary>
        public DragContext CreateValidationContext(BaseSlot targetBaseSlot, int previewCount, IItemAdapter previewItemAdapter = null)
        {
            if (TransferItemConversionUtility.TryCreateSourceStack(this, previewCount, out var sourceStack))
            {
                var sourceContext = Context ?? new DragContext(sourceStack, SourceBaseSlot, SourceInventory);
                var sourceEntry = new DragEntry(
                    sourceStack,
                    SourceBaseSlot,
                    SourceInventory,
                    SourceEntry?.SourcePlacement,
                    SourceEntry?.GrabOffset,
                    SourceEntry?.Orientation,
                    SourceEntry?.OrientationTopology);

                return sourceContext
                    .WithEntries(new[] { sourceEntry })
                    .WithTarget(targetBaseSlot, TargetInventory);
            }

            var stack = CreatePreviewStack(previewCount, previewItemAdapter);
            if (stack == null)
                return null;

            return new DragContext(stack, SourceBaseSlot, SourceInventory, targetBaseSlot, TargetInventory);
        }
    }
}
