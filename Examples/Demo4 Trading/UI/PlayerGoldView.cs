using System;
using UniversalDragAndDrop.Examples.Trading.Data;
using UnityEngine;
using UnityEngine.UI;

namespace UniversalDragAndDrop.Examples.Trading.UI
{
    public class PlayerGoldView : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefix for displaying money")]
        private string _moneyPrefix = "Gold: ";
        
        [SerializeField, Tooltip("Text for displaying the player's money")]
        // Replace to TMP Support
        // private TMPro.TMP_Text _moneyText;
        private Text _moneyText;

        [SerializeField, Tooltip("Suffix for displaying money")]
        private string _moneySuffix = "g";
        
        private void OnEnable()
        {
            UpdateMoneyUI();
            TradingEconomyManager.AutoCreateInstance.PlayerData.OnMoneyChanged += UpdateMoneyUI;
        }

        private void OnDisable()
        {
            if (TradingEconomyManager.IsInstanceExist)
                TradingEconomyManager.Instance.PlayerData.OnMoneyChanged -= UpdateMoneyUI;
        }

        private void UpdateMoneyUI()
        {
            if (_moneyText != null)
                _moneyText.text = $"{_moneyPrefix}{TradingEconomyManager.AutoCreateInstance.PlayerData.Money}{_moneySuffix}";
        }
    }
}