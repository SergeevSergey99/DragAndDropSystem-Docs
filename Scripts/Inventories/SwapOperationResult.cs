using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Результат операции обмена слотов. Содержит копии стаков до и после обмена.
    /// </summary>
    public readonly struct SwapOperationResult
    {
        public SwapOperationResult(
            ItemStack targetStackBefore,
            ItemStack sourceStackBefore,
            ItemStack targetStackAfter,
            ItemStack sourceStackAfter)
        {
            TargetStackBefore = targetStackBefore;
            SourceStackBefore = sourceStackBefore;
            TargetStackAfter = targetStackAfter;
            SourceStackAfter = sourceStackAfter;
        }

        public ItemStack TargetStackBefore { get; }
        public ItemStack SourceStackBefore { get; }
        public ItemStack TargetStackAfter { get; }
        public ItemStack SourceStackAfter { get; }

        public bool HasData =>
            TargetStackBefore != null &&
            SourceStackBefore != null &&
            TargetStackAfter != null &&
            SourceStackAfter != null;
    }
}
