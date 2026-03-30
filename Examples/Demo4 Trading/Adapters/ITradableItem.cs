using DragAndDropSystem.Examples.Trading.Data;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Общий интерфейс для торговых предметов.
    /// Реализуется обоими адаптерами (TradableSoAdapter и TradableItemAdapterModelAdapter),
    /// позволяя работать с ценами и типом предмета без привязки к конкретному адаптеру.
    /// </summary>
    public interface ITradableItem
    {
        ItemType ItemType { get; }
        int BuyPrice { get; }
        int SellPrice { get; }
        TradableItemSO OriginalSO { get; }
    }
}
