using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using Plugins.DragAndDropSystem.Examples.Trading.Data;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Приводит торговые предметы к model-адаптеру игрока.
    /// </summary>
    public sealed class ModelItemAdapterConverter : IItemAdapterConverter
    {
        public bool TryConvertIncoming(IItemAdapter itemAdapter, out IItemAdapter converted)
        {
            switch (itemAdapter)
            {
                case TradableItemAdapterModelAdapter:
                    converted = itemAdapter;
                    return true;
                case ITradableItem tradable:
                    converted = new TradableItemAdapterModelAdapter(new TradableItemModel(tradable.OriginalSO));
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
