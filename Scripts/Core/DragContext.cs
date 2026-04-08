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
        public BaseSlot SourceBaseSlot { get; }
        public IInventory SourceInventory { get; }

        public DragEntry(ItemStack stack, BaseSlot sourceBaseSlot, IInventory sourceInventory)
        {
            Stack = stack;
            SourceBaseSlot = sourceBaseSlot;
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
        public BaseSlot TargetBaseSlot { get; set; }

        public IInventory TargetInventory { get; set; }

        /// <summary>
        /// True if we have any target (slot or inventory).
        /// For world drops, both may be null - use processor-based validation instead.
        /// </summary>
        public bool HasTarget => TargetBaseSlot != null || TargetInventory != null;

        /// <summary>
        /// True if we have a specific target slot
        /// </summary>
        public bool HasTargetSlot => TargetBaseSlot != null;

        /// <summary>
        /// True if we have a target inventory
        /// </summary>
        public bool HasTargetInventory => TargetInventory != null;

        /// <summary>
        /// Конструктор для одиночного entry (основной сценарий)
        /// </summary>
        public DragContext(ItemStack stack, BaseSlot sourceBaseSlot, IInventory sourceInventory)
        {
            Entries = new[] { new DragEntry(stack, sourceBaseSlot, sourceInventory) };
        }
        /// <summary>
        /// Конструктор для одиночного entry с целью (например, вызов из кода с заранее известной целью)
        /// </summary>
        public DragContext(ItemStack stack, BaseSlot sourceBaseSlot, IInventory sourceInventory, BaseSlot targetBaseSlot, IInventory targetInventory)
        {
            Entries = new[] { new DragEntry(stack, sourceBaseSlot, sourceInventory) };
            SetTarget(targetBaseSlot, targetInventory);
        }

        /// <summary>
        /// Конструктор для множественных entries (batch drag)
        /// </summary>
        public DragContext(IReadOnlyList<DragEntry> entries)
        {
            Entries = entries;
        }

        private DragContext(IReadOnlyList<DragEntry> entries, BaseSlot targetBaseSlot, IInventory targetInventory)
        {
            Entries = entries;
            TargetBaseSlot = targetBaseSlot;
            TargetInventory = targetInventory;
        }

        /// <summary>
        /// Создаёт копию контекста с заданной целью для валидации правил.
        /// Оригинальный контекст не изменяется.
        /// </summary>
        public DragContext WithTarget(BaseSlot targetBaseSlot, IInventory targetInventory)
            => new DragContext(Entries, targetBaseSlot, targetInventory);

        public void SetTarget(BaseSlot targetBaseSlot, IInventory targetInventory)
        {
            TargetBaseSlot = targetBaseSlot;
            TargetInventory = targetInventory;
        }

        public void ClearTarget()
        {
            TargetBaseSlot = null;
            TargetInventory = null;
        }
    }
}
