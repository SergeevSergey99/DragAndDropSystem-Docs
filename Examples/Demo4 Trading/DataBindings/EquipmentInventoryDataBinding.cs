using System.Collections.Generic;
using System.Linq;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря экипировки игрока.
    /// Использует MappedSlotInventoryDataBinding для декларативной привязки слотов к данным.
    /// Conversion вынесен в отдельный inventory-side converter.
    /// </summary>
    public class EquipmentInventoryDataBinding : MappedSlotInventoryDataBinding<TradableItemModel, TradableItemAdapterModelAdapter>, ITransferDomainHandler
    {
        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("Weapon slot")]
        private ISlot _weaponSlot;

        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("Armor slot")]
        private ISlot _armorSlot;

        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("First artifact slot")]
        private ISlot _artifactSlot;

        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("Second artifact slot")]
        private ISlot _posionsSlot;

        private PlayerData PlayerData => TradingEconomyManager.AutoCreateInstance.PlayerData;

        protected override TradableItemAdapterModelAdapter CreateAdapter(TradableItemModel item) => new(item);
        protected override IItemAdapterConverter CreateItemConverter() => new ModelItemAdapterConverter();

        // --- MappedSlotInventoryDataBinding примитивы ---

        protected override Dictionary<ISlot, SlotBinding<TradableItemModel, TradableItemAdapterModelAdapter>> CreateBindingMap() => new()
        {
            [_weaponSlot] = new(
                get: () => PlayerData.EquippedWeapon,
                set: adapter => PlayerData.EquipWeapon(adapter.Item),
                clear: () => PlayerData.UnequipWeapon(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Weapon
                    && adapter.ItemId != _weaponSlot.Stack.ID // block same item
                    ? RuleResult.Success()
                    : RuleResult.Failure("Only weapons can be placed in this slot")),

            [_armorSlot] = new(
                get: () => PlayerData.EquippedArmor,
                set: adapter => PlayerData.EquipArmor(adapter.Item),
                clear: () => PlayerData.UnequipArmor(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Armor 
                    && adapter.ItemId != _armorSlot.Stack.ID // block same item
                    ? RuleResult.Success()
                    : RuleResult.Failure("Only armor can be placed in this slot")),

            [_artifactSlot] = new(
                get: () => PlayerData.EquippedArtifact,
                set: adapter => PlayerData.EquipArtifact1(adapter.Item),
                clear: () => PlayerData.UnequipArtifact1(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Artifact
                    && adapter.ItemId != _artifactSlot.Stack.ID // block same item
                    ? RuleResult.Success()
                    : RuleResult.Failure("Only artifacts can be placed in this slot")),
            
            [_posionsSlot] = new(
                getAll: () => PlayerData.EquippedPotions,
                // for each adapter in adapters
                add: adapters => adapters.ToList().ForEach(adapter => PlayerData.AddPotion(adapter.Item)),
                remove: adapters => adapters.ToList().ForEach(adapter => PlayerData.RemovePotion(adapter.Item)),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Potion
                    ? RuleResult.Success()
                    : RuleResult.Failure("Only potions can be placed in this slot")),
        };

        public RuleResult CanCommitTransfer(TransferDomainContext context) => TradingHelper.ValidatePlayerTransfer(context, PlayerData);

        public void OnTransferSucceeded(TransferDomainContext context) => TradingHelper.ApplyPlayerTransferEffects(context, PlayerData);
    }
}
