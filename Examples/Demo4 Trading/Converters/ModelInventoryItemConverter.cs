using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using Plugins.DragAndDropSystem.Examples.Trading.Data;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Приводит торговые предметы к model-адаптеру игрока.
    /// </summary>
    public sealed class ModelInventoryItemConverter : IInventoryItemConverter
    {
        public bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted)
        {
            switch (item)
            {
                case TradableItemModelAdapter:
                    converted = item;
                    return true;
                case ITradableItem tradable:
                    converted = new TradableItemModelAdapter(new TradableItemModel(tradable.OriginalSO));
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
