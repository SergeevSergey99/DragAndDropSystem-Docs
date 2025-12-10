using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;

namespace DragAndDropSystem.Inventories
{
    public readonly struct InventoryTransferRequest
    {
        public InventoryTransferRequest(
            IInventory sourceInventory,
            ISlot sourceSlot,
            IInventory targetInventory,
            ISlot targetSlot,
            ItemStack draggedStack,
            bool allowAlternativeSlots)
        {
            SourceInventory = sourceInventory;
            SourceSlot = sourceSlot;
            TargetInventory = targetInventory;
            TargetSlot = targetSlot;
            DraggedStack = draggedStack;
            AllowAlternativeSlots = allowAlternativeSlots;
        }

        public IInventory SourceInventory { get; }
        public ISlot SourceSlot { get; }
        public IInventory TargetInventory { get; }
        public ISlot TargetSlot { get; }
        public ItemStack DraggedStack { get; }
        public bool AllowAlternativeSlots { get; }

        public bool IsValid =>
            SourceInventory != null &&
            SourceSlot != null &&
            TargetInventory != null &&
            DraggedStack != null &&
            DraggedStack.Item != null &&
            DraggedStack.Count > 0;
    }

    public readonly struct InventoryTransferResult
    {
        public InventoryTransferResult(
            IInventory sourceInventory,
            IInventory targetInventory,
            ISlot sourceSlot,
            ISlot targetSlot,
            IInventoryItem item,
            int amount,
            bool targetWasEmptyBefore,
            int remainingInSource = 0)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
            Item = item;
            Amount = amount;
            TargetWasEmptyBefore = targetWasEmptyBefore;
            RemainingInSource = remainingInSource;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public ISlot SourceSlot { get; }
        public ISlot TargetSlot { get; }
        public IInventoryItem Item { get; }
        /// <summary>
        /// Количество предметов, которые были успешно перенесены
        /// </summary>
        public int Amount { get; }
        public bool TargetWasEmptyBefore { get; }
        /// <summary>
        /// Количество предметов, оставшихся в исходном слоте (при частичном переносе)
        /// </summary>
        public int RemainingInSource { get; }
        /// <summary>
        /// Был ли перенос частичным (не все предметы перенесены)
        /// </summary>
        public bool IsPartialTransfer => RemainingInSource > 0;
    }

    /// <summary>
    /// Сервис, выполняющий транзакционный перенос предметов между инвентарями.
    /// Отвечает за снятие предмета, размещение в цели и откат при неудаче.
    /// Поддерживает частичные переносы: если целевой инвентарь может принять только часть предметов,
    /// перенесёт столько, сколько возможно, и оставит остаток в исходном слоте.
    /// </summary>
    public class InventoryTransferService
    {
        public bool TryExecuteTransfer(InventoryTransferRequest request, out InventoryTransferResult result)
        {
            result = default;

            if (!request.IsValid)
            {
                Extentions.DragAndDropLog("<color=red>[InventoryTransferService] Invalid request</color>");
                return false;
            }

            var sourceInventory = request.SourceInventory;
            var targetInventory = request.TargetInventory;
            var sourceSlot = request.SourceSlot;
            var targetSlot = request.TargetSlot;
            var draggedStack = request.DraggedStack;

            var sourceSnapshotProvider = sourceInventory as IInventorySnapshotProvider;
            var targetSnapshotProvider = targetInventory as IInventorySnapshotProvider;

            var sourceInventorySnapshot = sourceSnapshotProvider?.CaptureSnapshot();
            var targetInventorySnapshot = targetSnapshotProvider?.CaptureSnapshot();
            var sourceSlotState = InventorySnapshotUtility.CaptureSlotState(sourceSlot);

            int requestedAmount = draggedStack.Count;
            var stackItem = draggedStack.Item;

            // Определяем, сколько предметов целевой инвентарь может принять
            int acceptableCount = targetInventory.GetAcceptableCount(stackItem, requestedAmount);

            if (acceptableCount <= 0)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryTransferService] Target inventory cannot accept any items</color>");
                return false;
            }

            // Определяем фактическое количество для переноса
            int transferAmount = System.Math.Min(requestedAmount, acceptableCount);
            int remainingAmount = requestedAmount - transferAmount;

            Extentions.DragAndDropLog($"<color=cyan>[InventoryTransferService] Requested: {requestedAmount}, Acceptable: {acceptableCount}, Transfer: {transferAmount}, Remaining: {remainingAmount}</color>");

            // Удаляем только то количество, которое можем перенести
            int removed = sourceSlot.Stack.RemoveFromStack(transferAmount);
            if (removed <= 0)
            {
                InventorySnapshotUtility.RestoreSlotState(sourceSlot, sourceSlotState);
                return false;
            }

            if (removed != transferAmount)
            {
                transferAmount = removed;
                remainingAmount = requestedAmount - transferAmount;
            }

            var transferStack = new ItemStack(stackItem, transferAmount);
            sourceSlot.UpdateVisuals();

            var operationContext = new SlotOperationContext { SuppressEvents = true };

            bool added = TryAddToTargetInventory(
                targetInventory,
                targetSlot,
                sourceInventory,
                sourceSlot,
                transferStack,
                transferAmount,
                targetInventorySnapshot,
                request.AllowAlternativeSlots,
                operationContext);

            if (!added)
            {
                Extentions.DragAndDropLog("<color=red>[InventoryTransferService] Failed to add to target, rolling back</color>");
                InventorySnapshotUtility.RestoreInventorySnapshot(sourceInventory, sourceSnapshotProvider, sourceInventorySnapshot, sourceSlot, sourceSlotState);
                InventorySnapshotUtility.RestoreInventorySnapshot(targetInventory, targetSnapshotProvider, targetInventorySnapshot, null, default);
                return false;
            }

            // Проверяем, остались ли предметы в transferStack (не все были добавлены)
            int actuallyAdded = transferAmount - (transferStack?.Count ?? 0);
            int actualRemaining = requestedAmount - actuallyAdded;

            // Если что-то осталось в transferStack, нужно вернуть в источник
            if (transferStack != null && !transferStack.IsEmpty)
            {
                Extentions.DragAndDropLog($"<color=yellow>[InventoryTransferService] {transferStack.Count} items not placed, returning to source</color>");
                if (sourceSlot.IsEmpty)
                {
                    sourceSlot.SetStack(new ItemStack(stackItem, transferStack.Count));
                }
                else
                {
                    sourceSlot.Stack.AddToStack(transferStack.Count);
                }
                sourceSlot.UpdateVisuals();
            }

            var resolvedSlot = operationContext.ResolvedSlot ?? targetSlot;
            bool targetWasEmpty = resolvedSlot != null && operationContext.TargetWasEmptyBefore;

            if (resolvedSlot == null && targetInventorySnapshot != null)
            {
                if (InventorySnapshotUtility.TryResolveSlotChange(targetInventory, targetInventorySnapshot, out var changedSlot, out var wasEmptyBefore))
                {
                    resolvedSlot = changedSlot;
                    targetWasEmpty = wasEmptyBefore;
                }
            }

            result = new InventoryTransferResult(
                sourceInventory,
                targetInventory,
                sourceSlot,
                resolvedSlot,
                stackItem,
                actuallyAdded,
                targetWasEmpty,
                actualRemaining);

            Extentions.DragAndDropLog($"<color=green>[InventoryTransferService] Transfer complete: {actuallyAdded} transferred, {actualRemaining} remaining in source</color>");

            return true;
        }

        private bool TryAddToTargetInventory(
            IInventory targetInventory,
            ISlot requestedSlot,
            IInventory sourceInventory,
            ISlot sourceSlot,
            ItemStack transferStack,
            int transferAmount,
            InventorySnapshot targetSnapshot,
            bool allowAlternativeSlots,
            SlotOperationContext operationContext)
        {
            if (targetInventory == null)
                return false;

            operationContext?.ResetResult();

            // Проверяем, нужно ли распределять предметы по нескольким слотам
            // Это необходимо для Unique инвентарей, когда переносим более 1 предмета
            bool needsDistribution = targetInventory is UniversalInventory univTarget
                && univTarget.ItemBehavior == UniversalInventory.ItemBehaviorType.Unique
                && transferStack.Count > 1;

            if (needsDistribution)
            {
                Extentions.DragAndDropLog($"<color=cyan>[InventoryTransferService] Using distribution mode for Unique inventory ({transferStack.Count} items)</color>");

                // Для Unique инвентарей используем TryAddStack, который распределит предметы по слотам через стратегию
                targetInventory.TryAddStack(transferStack, -1);

                // TryAddStack уменьшает transferStack по мере добавления
                // Проверяем, добавилось ли хоть что-то
                int added = transferAmount - transferStack.Count;
                if (added > 0)
                {
                    if (operationContext != null && InventorySnapshotUtility.TryResolveSlotChange(targetInventory, targetSnapshot, out var slot, out var wasEmptyBefore))
                    {
                        operationContext.RecordResult(slot, wasEmptyBefore, added);
                    }
                    return true; // Частичный или полный успех
                }
                return false;
            }

            if (requestedSlot != null)
            {
                bool wasEmpty = requestedSlot.IsEmpty;
                if (targetInventory.TryAddToSlot(transferStack, requestedSlot, sourceInventory, sourceSlot.Index, operationContext))
                {
                    if (operationContext?.ResolvedSlot == null)
                    {
                        operationContext?.RecordResult(requestedSlot, wasEmpty, transferAmount);
                    }
                    return transferStack.IsEmpty;
                }

                // Целевой слот не принял предмет. Пытаемся найти альтернативный слот с полной валидацией правил.
                if (allowAlternativeSlots && targetInventory is UniversalInventory universalInventory && transferStack != null && !transferStack.IsEmpty)
                {
                    // Ищем альтернативный слот с проверкой ВСЕХ правил (включая inventory-level и DataBinding)
                    var alternativeSlot = FindValidAlternativeSlot(universalInventory, transferStack, sourceInventory, sourceSlot);

                    if (alternativeSlot != null)
                    {
                        Extentions.DragAndDropLog($"<color=cyan>[InventoryTransferService] Found valid alternative slot {alternativeSlot.Index}</color>");
                        operationContext?.ResetResult();
                        bool altWasEmpty = alternativeSlot.IsEmpty;
                        if (targetInventory.TryAddToSlot(transferStack, alternativeSlot, sourceInventory, sourceSlot.Index, operationContext))
                        {
                            if (operationContext?.ResolvedSlot == null)
                            {
                                operationContext?.RecordResult(alternativeSlot, altWasEmpty, transferAmount);
                            }
                            return transferStack.IsEmpty;
                        }
                    }
                    else
                    {
                        Extentions.DragAndDropLog("<color=yellow>[InventoryTransferService] No valid alternative slot found</color>");
                    }
                }
            }
            else if (targetInventory.TryAddStack(transferStack, -1))
            {
                // TryAddStack может добавить частично (для Unique стратегии)
                int added = transferAmount - transferStack.Count;
                if (added > 0)
                {
                    if (operationContext != null && InventorySnapshotUtility.TryResolveSlotChange(targetInventory, targetSnapshot, out var slot, out var wasEmptyBefore))
                    {
                        operationContext.RecordResult(slot, wasEmptyBefore, added);
                    }
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ищет альтернативный слот для размещения предмета с полной валидацией правил.
        /// Проверяет как slot-level правила, так и inventory-level правила (включая DataBinding).
        /// </summary>
        private ISlot FindValidAlternativeSlot(
            UniversalInventory targetInventory,
            ItemStack transferStack,
            IInventory sourceInventory,
            ISlot sourceSlot)
        {
            if (targetInventory == null || transferStack == null || transferStack.IsEmpty)
                return null;

            // Создаем контекст для валидации правил
            var validationContext = new DragContext(transferStack, sourceSlot, sourceInventory);

            foreach (var slot in targetInventory.Slots)
            {
                // Пропускаем занятые слоты (для Unique) или слоты с несовместимыми предметами
                if (!slot.IsEmpty)
                {
                    // Для стакуемых режимов проверяем можно ли стакать
                    if (targetInventory.ItemBehavior == UniversalInventory.ItemBehaviorType.Unique)
                        continue;

                    if (!slot.Stack.CanStack(transferStack.Item))
                        continue;
                }

                // Устанавливаем целевой слот в контексте для валидации
                validationContext.SetTarget(slot, targetInventory);

                // Проверяем inventory-level правила (включая DataBinding правила)
                var inventoryResult = targetInventory.RuleValidator.ValidateDrop(validationContext);
                if (!inventoryResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=gray>[InventoryTransferService] Slot {slot.Index} rejected by inventory rules: {inventoryResult.FailureReason}</color>");
                    continue;
                }

                // Проверяем slot-level правила
                if (slot.SlotRuleValidator != null)
                {
                    var slotResult = slot.SlotRuleValidator.ValidateDrop(validationContext);
                    if (!slotResult.IsValid)
                    {
                        Extentions.DragAndDropLog($"<color=gray>[InventoryTransferService] Slot {slot.Index} rejected by slot rules: {slotResult.FailureReason}</color>");
                        continue;
                    }
                }

                // Нашли подходящий слот!
                return slot;
            }

            return null;
        }
    }
}
