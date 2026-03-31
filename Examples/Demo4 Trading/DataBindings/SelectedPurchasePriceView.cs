using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Показывает суммарную стоимость выделенных предметов для покупки.
    /// Учитывает только слоты из инвентарей продавцов (MerchantInventoryDataBinding).
    /// </summary>
    public class SelectedPurchasePriceView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _totalPriceText;

        [Header("Text Format")]
        [SerializeField] private string _prefix = "Buy Total: ";
        [SerializeField] private string _suffix = "g";

        private void OnEnable()
        {
            SelectionManager.OnSelectionChanged += Refresh;
            if (SelectionManager.IsInstanceExist)
                Refresh(SelectionManager.AutoCreateInstance.CurrentContext);
        }

        private void OnDisable()
        {
            SelectionManager.OnSelectionChanged -= Refresh;
        }

        private void Refresh(SelectionContext context)
        {
            int totalPrice = CalculateSelectedPurchaseTotal(context);
            UpdateText(totalPrice);
        }

        private int CalculateSelectedPurchaseTotal(SelectionContext context)
        {
            if (context == null || !context.HasSelection)
                return 0;

            int total = 0;

            foreach (var inventoryEntry in context.ByInventory)
            {
                var inventory = inventoryEntry.Key;
                if (inventory == null || inventory.DataBinding is not IMerchantInventory)
                    continue;

                var selectedSlots = inventoryEntry.Value;
                if (selectedSlots == null)
                    continue;

                foreach (var slot in selectedSlots)
                {
                    total += GetSlotPurchasePrice(slot);
                }
            }

            return total;
        }

        private static int GetSlotPurchasePrice(ISlot slot)
        {
            if (slot == null || slot.IsEmpty || slot.Stack == null || slot.Stack.PrimaryAdapter == null)
                return 0;

            int unitPrice = 0;
            if (slot.Stack.PrimaryAdapter is ITradableItem tradable)
            {
                unitPrice = tradable.BuyPrice;
            }

            if (unitPrice <= 0)
                return 0;

            return unitPrice * slot.Stack.Count;
        }

        private void UpdateText(int totalPrice)
        {
            if (_totalPriceText == null)
                return;

            _totalPriceText.text = $"{_prefix}{totalPrice}{_suffix}";
        }
    }
}
