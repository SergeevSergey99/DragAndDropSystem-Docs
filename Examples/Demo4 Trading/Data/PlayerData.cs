using System;
using System.Collections.Generic;
using DragAndDropSystem.Tools.Inspector;
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
        [SerializeReference, ManagedReferencePicker]
        private TradableItemModel _equippedWeapon;

        [SerializeReference, ManagedReferencePicker]
        private TradableItemModel _equippedArmor;

        [SerializeReference, ManagedReferencePicker]
        private TradableItemModel _equippedArtifact;

        [SerializeReference, ManagedReferencePicker]
        private List<TradableItemModel> _equippedPotions = new();

        public TradableItemModel EquippedWeapon => _equippedWeapon;
        public TradableItemModel EquippedArmor => _equippedArmor;
        public TradableItemModel EquippedArtifact => _equippedArtifact;
        public List<TradableItemModel> EquippedPotions => _equippedPotions;

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
            Debug.Log($"Trying to remove item: {item?.originalSO?.name ?? "null"}");
            if (_inventory.Contains(item))
            {
                Debug.Log("Item found in inventory, removing...");
                _inventory.Remove(item);
                OnInventoryChanged?.Invoke();
                return true;
            }
            Debug.Log("Item not found in inventory, cannot remove.");
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
            _equippedArtifact = item;
            OnEquipmentChanged?.Invoke();
        }

        public void UnequipArtifact1()
        {
            _equippedArtifact = null;
            OnEquipmentChanged?.Invoke();
        }

        public void AddPotion(TradableItemModel item)
        {
            _equippedPotions.Add(item);
            OnEquipmentChanged?.Invoke();
        }

        public void RemovePotion(TradableItemModel item)
        {
            _equippedPotions.Remove(item);
            OnEquipmentChanged?.Invoke();
        }

        #endregion
    }
}
