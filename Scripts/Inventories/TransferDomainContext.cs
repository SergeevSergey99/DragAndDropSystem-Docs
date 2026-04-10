using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Domain-operation context for a single planned transfer allocation.
    /// Created before commit and then reused after the transfer completes successfully.
    /// </summary>
    public sealed class TransferDomainContext
    {
        public TransferDomainContext(
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot plannedTargetBaseSlot,
            IItemAdapter sourceItemAdapter,
            IItemAdapter previewTargetItemAdapter,
            int requestedAmount,
            TransferKind kind)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            PlannedTargetBaseSlot = plannedTargetBaseSlot;
            SourceItemAdapter = sourceItemAdapter;
            PreviewTargetItemAdapter = previewTargetItemAdapter ?? sourceItemAdapter;
            RequestedAmount = requestedAmount;
            TargetBaseSlot = plannedTargetBaseSlot;
            TargetItemAdapter = PreviewTargetItemAdapter;
            CommittedAmount = requestedAmount;
            Kind = kind;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public InventoryDataBindingBase SourceBinding => SourceInventory?.DataBinding;
        public InventoryDataBindingBase TargetBinding => TargetInventory?.DataBinding;
        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot PlannedTargetBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; private set; }
        public IItemAdapter SourceItemAdapter { get; }
        public IItemAdapter PreviewTargetItemAdapter { get; }
        public IItemAdapter TargetItemAdapter { get; private set; }
        public int RequestedAmount { get; }
        public int CommittedAmount { get; private set; }
        public bool IsCommitted { get; private set; }
        public TransferKind Kind { get; }
        public TransferDomainContext CounterpartContext { get; internal set; }

        public void MarkCommitted(InventoryTransferResult outcome)
        {
            IsCommitted = true;
            TargetBaseSlot = outcome.TargetBaseSlot ?? PlannedTargetBaseSlot;
            TargetItemAdapter = outcome.TargetItemAdapter ?? PreviewTargetItemAdapter;
            CommittedAmount = outcome.Amount;
        }

        public void MarkCommitted(BaseSlot targetBaseSlot, IItemAdapter targetItemAdapter, int committedAmount)
        {
            IsCommitted = true;
            TargetBaseSlot = targetBaseSlot ?? PlannedTargetBaseSlot;
            TargetItemAdapter = targetItemAdapter ?? PreviewTargetItemAdapter;
            CommittedAmount = committedAmount;
        }
    }
}
