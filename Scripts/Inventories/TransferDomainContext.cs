using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Контекст доменной операции для одного planned transfer allocation.
    /// Создается до commit и затем переиспользуется после успешного завершения переноса.
    /// </summary>
    public sealed class TransferDomainContext
    {
        public TransferDomainContext(
            IInventory sourceInventory,
            IInventory targetInventory,
            ISlot sourceSlot,
            ISlot plannedTargetSlot,
            IItemAdapter sourceItemAdapter,
            IItemAdapter previewTargetItemAdapter,
            int requestedAmount,
            TransferKind kind)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceSlot = sourceSlot;
            PlannedTargetSlot = plannedTargetSlot;
            SourceItemAdapter = sourceItemAdapter;
            PreviewTargetItemAdapter = previewTargetItemAdapter ?? sourceItemAdapter;
            RequestedAmount = requestedAmount;
            TargetSlot = plannedTargetSlot;
            TargetItemAdapter = PreviewTargetItemAdapter;
            CommittedAmount = requestedAmount;
            Kind = kind;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public InventoryDataBindingBase SourceBinding => SourceInventory?.DataBinding;
        public InventoryDataBindingBase TargetBinding => TargetInventory?.DataBinding;
        public ISlot SourceSlot { get; }
        public ISlot PlannedTargetSlot { get; }
        public ISlot TargetSlot { get; private set; }
        public IItemAdapter SourceItemAdapter { get; }
        public IItemAdapter PreviewTargetItemAdapter { get; }
        public IItemAdapter TargetItemAdapter { get; private set; }
        public int RequestedAmount { get; }
        public int CommittedAmount { get; private set; }
        public bool IsCommitted { get; private set; }
        public TransferKind Kind { get; }

        public void MarkCommitted(InventoryTransferResult outcome)
        {
            IsCommitted = true;
            TargetSlot = outcome.TargetSlot ?? PlannedTargetSlot;
            TargetItemAdapter = outcome.TargetItemAdapter ?? PreviewTargetItemAdapter;
            CommittedAmount = outcome.Amount;
        }

        public void MarkCommitted(ISlot targetSlot, IItemAdapter targetItemAdapter, int committedAmount)
        {
            IsCommitted = true;
            TargetSlot = targetSlot ?? PlannedTargetSlot;
            TargetItemAdapter = targetItemAdapter ?? PreviewTargetItemAdapter;
            CommittedAmount = committedAmount;
        }
    }
}
