using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Приводит входящие торговые предметы к SO-адаптеру торговца.
    /// </summary>
    public sealed class MerchantInventoryItemConverter : IInventoryItemConverter
    {
        public bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted)
        {
            switch (item)
            {
                case TradableSoAdapter:
                    converted = item;
                    return true;
                case ITradableItem tradable:
                    converted = new TradableSoAdapter(tradable.OriginalSO);
                    return true;
                default:
                    converted = null;
                    return false;
            }
        }

        public bool TryConvertOutgoing(IInventoryItem item, out IInventoryItem converted)
        {
            converted = item;
            return item != null;
        }
    }
}
