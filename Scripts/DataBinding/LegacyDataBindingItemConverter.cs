using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// Временный bridge для старых DataBinding, где conversion еще не вынесен в отдельный класс.
    /// </summary>
    internal sealed class LegacyDataBindingItemConverter : IInventoryItemConverter
    {
        private readonly InventoryDataBindingBase _binding;

        public LegacyDataBindingItemConverter(InventoryDataBindingBase binding)
        {
            _binding = binding;
        }

        public bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted)
        {
            converted = _binding.ConvertIncomingItem(item);
            return converted != null;
        }

        public bool TryConvertOutgoing(IInventoryItem item, out IInventoryItem converted)
        {
            converted = _binding.ConvertOutgoingItem(item);
            return converted != null;
        }
    }
}
