using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Конвертер по умолчанию: пропускает предметы без изменений.
    /// </summary>
    public sealed class IdentityItemAdapterConverter : IItemAdapterConverter
    {
        public static readonly IdentityItemAdapterConverter Instance = new();

        private IdentityItemAdapterConverter()
        {
        }

        public bool TryConvertIncoming(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            converted = itemAdapter;
            return itemAdapter != null;
        }

        public bool TryConvertOutgoing(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            converted = itemAdapter;
            return itemAdapter != null;
        }
    }
}
