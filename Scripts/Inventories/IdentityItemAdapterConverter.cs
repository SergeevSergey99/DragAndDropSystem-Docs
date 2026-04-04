using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Конвертер по умолчанию: пропускает предметы без изменений.
    /// </summary>
    public sealed class IdentityItemAdapterConverter : IItemAdapterConverter
    {
        public static readonly IdentityItemAdapterConverter Instance = new();

        public IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter) => itemAdapter;
        public IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter) => itemAdapter;
    }
}
