using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Результат операции обмена слотов. Содержит копии стаков до обмена.
    /// </summary>
    public readonly struct SwapOperationResult
    {
        public SwapOperationResult(ItemStack targetStackBefore, ItemStack sourceStackBefore)
        {
            TargetStackBefore = targetStackBefore;
            SourceStackBefore = sourceStackBefore;
        }

        public ItemStack TargetStackBefore { get; }
        public ItemStack SourceStackBefore { get; }

        public bool HasData => TargetStackBefore != null && SourceStackBefore != null;
    }
}
