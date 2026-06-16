using UnityEngine;
using UnityEngine.UI;
using UDND.Core;
using UDND.Selection;
using UDND.Slots;

namespace UDND.Examples.Trading
{
    /// <summary>
    /// Shows the total purchase price of selected items.
    /// Only counts slots from seller inventories (MerchantInventoryDataBinding).
    /// </summary>
    public class SelectedPurchasePriceView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField]
        // Replace to TMP Support
        // private TMPro.TMP_Text _totalPriceText;
        private Text _totalPriceText;

        [Header("Text Format")]
        [SerializeField] private string _prefix = "Buy Total: ";
        [SerializeField] private string _suffix = "g";

        private void OnEnable()
        {
            UDNDEvents.OnSelectionChanged += Refresh;
            if (SelectionManager.IsInstanceExist)
                Refresh(SelectionManager.AutoCreateInstance.CurrentContext);
        }

        private void OnDisable()
        {
            UDNDEvents.OnSelectionChanged -= Refresh;
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

        private static int GetSlotPurchasePrice(BaseSlot baseSlot)
        {
            if (baseSlot == null || baseSlot.IsEmpty || baseSlot.Stack == null || baseSlot.Stack.PrimaryAdapter == null)
                return 0;

            int unitPrice = 0;
            if (baseSlot.Stack.PrimaryAdapter is ITradableItem tradable)
            {
                unitPrice = tradable.BuyPrice;
            }

            if (unitPrice <= 0)
                return 0;

            return unitPrice * baseSlot.Stack.Count;
        }

        private void UpdateText(int totalPrice)
        {
            if (_totalPriceText == null)
                return;

            _totalPriceText.text = $"{_prefix}{totalPrice}{_suffix}";
        }
    }
}