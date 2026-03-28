using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Examples.Trading.Data
{
    /// <summary>
    /// Данные торговца
    /// </summary>
    [Serializable]
    public class MerchantData
    {
        [field: SerializeField]
        public string DisplayName { get; private set; }
        [field: SerializeField]
        public int Money { get; private set; } = 500;
        [SerializeField]
        private List<TradableItemSO> _inventory = new List<TradableItemSO>();
        
        public IReadOnlyList<TradableItemSO> Inventory => _inventory.AsReadOnly();

        // События для синхронизации с UI
        public event Action OnMoneyChanged;
        public event Action OnInventoryChanged;

        public void AddMoney(int amount)
        {
            Money += amount;
            OnMoneyChanged?.Invoke();
        }

        public bool TrySpendMoney(int amount)
        {
            if (Money >= amount)
            {
                Money -= amount;
                OnMoneyChanged?.Invoke();
                return true;
            }
            return false;
        }
        
        public void AddItem(TradableItemSO item)
        {
            _inventory.Add(item);
            OnInventoryChanged?.Invoke();
        }

        public bool TryRemoveItem(TradableItemSO item)
        {
            var existingStack = _inventory.Find(s => s == item);
            if (existingStack != null)
            {
                _inventory.Remove(existingStack);
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        public int GetItemCount(TradableItemSO item)
        {
             return _inventory.FindAll(s => s == item).Count;
        }
    }
}