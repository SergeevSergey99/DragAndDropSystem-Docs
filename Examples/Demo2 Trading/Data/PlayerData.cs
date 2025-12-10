using System;
using System.Collections.Generic;
using DragAndDropSystem.Examples.Trading;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples.Trading.Data
{
    
    /// <summary>
    /// Данные экономики игрока
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        [field: SerializeField]
        public int Money { get; private set; } = 1000;

        [SerializeField]
        private List<TradableItemModel> _inventory = new List<TradableItemModel>();

        public IReadOnlyList<TradableItemModel> Inventory => _inventory.AsReadOnly();

        [Header("Equipment")]
        [SerializeReference]
        private TradableItemModel _equippedWeapon;

        [SerializeReference]
        private TradableItemModel _equippedArmor;

        [SerializeReference]
        private TradableItemModel _equippedArtifact1;

        [SerializeReference]
        private TradableItemModel _equippedArtifact2;

        public TradableItemModel EquippedWeapon => _equippedWeapon;
        public TradableItemModel EquippedArmor => _equippedArmor;
        public TradableItemModel EquippedArtifact1 => _equippedArtifact1;
        public TradableItemModel EquippedArtifact2 => _equippedArtifact2;

        public event Action OnMoneyChanged;
        public event Action OnInventoryChanged;
        public event Action OnEquipmentChanged;

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

        public void AddItem(TradableItemModel item)
        {
            _inventory.Add(item);
            OnInventoryChanged?.Invoke();
        }

        public bool TryRemoveItem(TradableItemModel item)
        {
            if (_inventory.Contains(item))
            {
                _inventory.Remove(item);
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        public int GetItemCount(TradableItemSO itemSo)
        {
            return _inventory.FindAll(s => s.originalSO == itemSo).Count;
        }

        #region Equipment Methods

        public void EquipWeapon(TradableItemModel item)
        {
            _equippedWeapon = item;
            OnEquipmentChanged?.Invoke();
        }

        public void UnequipWeapon()
        {
            _equippedWeapon = null;
            OnEquipmentChanged?.Invoke();
        }

        public void EquipArmor(TradableItemModel item)
        {
            _equippedArmor = item;
            OnEquipmentChanged?.Invoke();
        }

        public void UnequipArmor()
        {
            _equippedArmor = null;
            OnEquipmentChanged?.Invoke();
        }

        public void EquipArtifact1(TradableItemModel item)
        {
            _equippedArtifact1 = item;
            OnEquipmentChanged?.Invoke();
        }

        public void UnequipArtifact1()
        {
            _equippedArtifact1 = null;
            OnEquipmentChanged?.Invoke();
        }

        public void EquipArtifact2(TradableItemModel item)
        {
            _equippedArtifact2 = item;
            OnEquipmentChanged?.Invoke();
        }

        public void UnequipArtifact2()
        {
            _equippedArtifact2 = null;
            OnEquipmentChanged?.Invoke();
        }

        #endregion
    }
}