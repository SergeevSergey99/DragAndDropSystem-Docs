using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Приводит входящие торговые предметы к SO-адаптеру торговца.
    /// </summary>
    public sealed class MerchantItemAdapterConverter : IItemAdapterConverter
    {
        public bool TryConvertIncoming(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            switch (itemAdapter)
            {
                case TradableSoAdapter:
                    converted = itemAdapter;
                    return true;
                case ITradableItem tradable:
                    converted = new TradableSoAdapter(tradable.OriginalSO);
                    return true;
                default:
                    converted = null;
                    return false;
            }
        }

        public bool TryConvertOutgoing(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            converted = itemAdapter;
            return itemAdapter != null;
        }
    }
}
