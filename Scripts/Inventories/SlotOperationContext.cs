using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Контекст операции добавления в слот.
    /// Позволяет получить фактический слот, куда был помещен предмет.
    /// </summary>
    public class SlotOperationContext
    {
        /// <summary>
        /// Фактический слот, куда был положен предмет (учитывая автослияние и переупаковку).
        /// </summary>
        public ISlot ResolvedSlot { get; private set; }

        /// <summary>
        /// Был ли слот пустым до операции (полезно для визуалов и анимаций).
        /// </summary>
        public bool TargetWasEmptyBefore { get; private set; }

        /// <summary>
        /// Количество предметов, которое реально оказалось в слоте.
        /// </summary>
        public int AddedCount { get; private set; }

        public void RecordResult(ISlot slot, bool wasEmptyBefore, int addedCount)
        {
            ResolvedSlot = slot;
            TargetWasEmptyBefore = wasEmptyBefore;
            AddedCount = addedCount;
        }

        public void ResetResult()
        {
            ResolvedSlot = null;
            TargetWasEmptyBefore = false;
            AddedCount = 0;
        }
    }
}
