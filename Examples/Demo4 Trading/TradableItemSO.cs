using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// ScriptableObject для торгового предмета с ценами покупки/продажи
    /// Используется в примере системы торговли с торговцами
    /// </summary>
    [CreateAssetMenu(fileName = "TradableItem", menuName = "DragAndDrop/Examples/Trading/TradableItemSO", order = 200)]
    public class TradableItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        [SerializeField] private string _displayName;
        [SerializeField, PreviewField(100)] private Sprite _icon;

        [SerializeField, Tooltip("Тип предмета (оружие, броня, артефакт и т.д.)")]
        private ItemType _itemType = ItemType.Other;

        [Header("Trading")]
        [SerializeField, Tooltip("Цена покупки у торговца (игрок платит)")]
        private int _buyPrice = 100;

        [SerializeField, Tooltip("Цена продажи торговцу (торговец платит)")]
        private int _sellPrice = 50;

        [Header("Description")]
        [SerializeField, TextArea(3, 5)]
        private string _description;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public ItemType ItemType => _itemType;
        public int BuyPrice => _buyPrice;
        public int SellPrice => _sellPrice;
        public string Description => _description;

        private void OnValidate()
        {
            // Проверяем что цена продажи не больше цены покупки
            if (_sellPrice > _buyPrice)
            {
                Debug.LogWarning($"[{name}] Цена продажи ({_sellPrice}) больше цены покупки ({_buyPrice})! Это может быть нелогично.");
            }
        }
    }
}
