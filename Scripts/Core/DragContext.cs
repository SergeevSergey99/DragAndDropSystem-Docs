using System.Collections.Generic;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Inventories;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Один элемент операции перетаскивания (источник + стак)
    /// </summary>
    public readonly struct DragEntry
    {
        public ItemStack Stack { get; }
        public ISlot SourceSlot { get; }
        public IInventory SourceInventory { get; }

        public DragEntry(ItemStack stack, ISlot sourceSlot, IInventory sourceInventory)
        {
            Stack = stack;
            SourceSlot = sourceSlot;
            SourceInventory = sourceInventory;
        }
    }

    /// <summary>
    /// Контекст операции перетаскивания
    /// Поддерживает как одиночный, так и множественный drag (batch)
    /// </summary>
    public class DragContext
    {
        public IReadOnlyList<DragEntry> Entries { get; }
        public bool IsBatchDrag => Entries.Count > 1;

        /// <summary>
        /// Целевой слот операции.
        /// <para>
        /// <b>null</b> — цель не задана (авто-перенос, дроп на область инвентаря, вызов из кода).<br/>
        /// <b>Single drag:</b> точный финальный слот — slot-правила применяются напрямую.<br/>
        /// <b>Batch drag:</b> UI-хинт (слот под курсором). Финальный слот каждого entry неизвестен
        /// до реального переноса — определяется execution pipeline (`TransferPlanExecutor`).
        /// Правила должны использовать <see cref="IsBatchDrag"/> чтобы игнорировать TargetSlot при batch-валидации.
        /// </para>
        /// </summary>
        public ISlot TargetSlot { get; set; }

        public IInventory TargetInventory { get; set; }

        /// <summary>
        /// True if we have any target (slot or inventory).
        /// For world drops, both may be null - use processor-based validation instead.
        /// </summary>
        public bool HasTarget => TargetSlot != null || TargetInventory != null;

        /// <summary>
        /// True if we have a specific target slot
        /// </summary>
        public bool HasTargetSlot => TargetSlot != null;

        /// <summary>
        /// True if we have a target inventory
        /// </summary>
        public bool HasTargetInventory => TargetInventory != null;

        /// <summary>
        /// Конструктор для одиночного entry (основной сценарий)
        /// </summary>
        public DragContext(ItemStack stack, ISlot sourceSlot, IInventory sourceInventory)
        {
            Entries = new[] { new DragEntry(stack, sourceSlot, sourceInventory) };
        }
        /// <summary>
        /// Конструктор для одиночного entry с целью (например, вызов из кода с заранее известной целью)
        /// </summary>
        public DragContext(ItemStack stack, ISlot sourceSlot, IInventory sourceInventory, ISlot targetSlot, IInventory targetInventory)
        {
            Entries = new[] { new DragEntry(stack, sourceSlot, sourceInventory) };
            SetTarget(targetSlot, targetInventory);
        }

        /// <summary>
        /// Конструктор для множественных entries (batch drag)
        /// </summary>
        public DragContext(IReadOnlyList<DragEntry> entries)
        {
            Entries = entries;
        }

        private DragContext(IReadOnlyList<DragEntry> entries, ISlot targetSlot, IInventory targetInventory)
        {
            Entries = entries;
            TargetSlot = targetSlot;
            TargetInventory = targetInventory;
        }

        /// <summary>
        /// Создаёт копию контекста с заданной целью для валидации правил.
        /// Оригинальный контекст не изменяется.
        /// </summary>
        public DragContext WithTarget(ISlot targetSlot, IInventory targetInventory)
            => new DragContext(Entries, targetSlot, targetInventory);

        public void SetTarget(ISlot targetSlot, IInventory targetInventory)
        {
            TargetSlot = targetSlot;
            TargetInventory = targetInventory;
        }

        public void ClearTarget()
        {
            TargetSlot = null;
            TargetInventory = null;
        }
    }
}
