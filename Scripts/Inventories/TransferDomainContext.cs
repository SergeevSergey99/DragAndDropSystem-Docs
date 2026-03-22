using DragAndDropSystem.Core;
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
            IInventoryItem sourceItem,
            IInventoryItem previewTargetItem,
            int requestedAmount)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceSlot = sourceSlot;
            PlannedTargetSlot = plannedTargetSlot;
            SourceItem = sourceItem;
            PreviewTargetItem = previewTargetItem ?? sourceItem;
            RequestedAmount = requestedAmount;
            TargetSlot = plannedTargetSlot;
            TargetItem = PreviewTargetItem;
            CommittedAmount = requestedAmount;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public ISlot SourceSlot { get; }
        public ISlot PlannedTargetSlot { get; }
        public ISlot TargetSlot { get; private set; }
        public IInventoryItem SourceItem { get; }
        public IInventoryItem PreviewTargetItem { get; }
        public IInventoryItem TargetItem { get; private set; }
        public int RequestedAmount { get; }
        public int CommittedAmount { get; private set; }
        public bool IsCommitted { get; private set; }

        public void MarkCommitted(InventoryTransferResult outcome)
        {
            IsCommitted = true;
            TargetSlot = outcome.TargetSlot ?? PlannedTargetSlot;
            TargetItem = outcome.TargetItem ?? PreviewTargetItem;
            CommittedAmount = outcome.Amount;
        }
    }
}
