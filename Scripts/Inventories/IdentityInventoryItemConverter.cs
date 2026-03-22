using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Конвертер по умолчанию: пропускает предметы без изменений.
    /// </summary>
    public sealed class IdentityInventoryItemConverter : IInventoryItemConverter
    {
        public static readonly IdentityInventoryItemConverter Instance = new();

        private IdentityInventoryItemConverter()
        {
        }

        public bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted)
        {
            converted = item;
            return item != null;
        }

        public bool TryConvertOutgoing(IInventoryItem item, out IInventoryItem converted)
        {
            converted = item;
            return item != null;
        }
    }
}
