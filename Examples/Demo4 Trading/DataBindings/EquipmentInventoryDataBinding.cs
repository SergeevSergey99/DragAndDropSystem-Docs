using System.Collections.Generic;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

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
        [SerializeField, Required, Tooltip("Слот для оружия")]
        private UniversalSlot _weaponSlot;

        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("Слот для брони")]
        private UniversalSlot _armorSlot;

        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("Первый слот для артефакта")]
        private UniversalSlot _artifact1Slot;

        [FoldoutGroup("Equipment Slots")]
        [SerializeField, Required, Tooltip("Второй слот для артефакта")]
        private UniversalSlot _artifact2Slot;

        private PlayerData PlayerData => TradingEconomyManager.AutoCreateInstance.PlayerData;
        protected override IItemAdapterConverter CreateItemConverter() => new ModelItemAdapterConverter();

        // --- MappedSlotInventoryDataBinding примитивы ---

        protected override Dictionary<ISlot, SlotBinding<TradableItemModel, TradableItemAdapterModelAdapter>> CreateBindingMap() => new()
        {
            [_weaponSlot] = new(
                get: () => PlayerData.EquippedWeapon,
                set: adapter => PlayerData.EquipWeapon(adapter.Item),
                clear: () => PlayerData.UnequipWeapon(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Weapon
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только оружие")),

            [_armorSlot] = new(
                get: () => PlayerData.EquippedArmor,
                set: adapter => PlayerData.EquipArmor(adapter.Item),
                clear: () => PlayerData.UnequipArmor(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Armor
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только броню")),

            [_artifact1Slot] = new(
                get: () => PlayerData.EquippedArtifact1,
                set: adapter => PlayerData.EquipArtifact1(adapter.Item),
                clear: () => PlayerData.UnequipArtifact1(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Artifact
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только артефакты")),

            [_artifact2Slot] = new(
                get: () => PlayerData.EquippedArtifact2,
                set: adapter => PlayerData.EquipArtifact2(adapter.Item),
                clear: () => PlayerData.UnequipArtifact2(),
                canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Artifact
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только артефакты")),
        };

        protected override TradableItemAdapterModelAdapter CreateAdapter(TradableItemModel item) => new(item);

        public RuleResult CanCommitTransfer(TransferDomainContext context) => TradingHelper.ValidatePlayerTransfer(context, PlayerData);

        public void OnTransferSucceeded(TransferDomainContext context) => TradingHelper.ApplyPlayerTransferEffects(context, PlayerData);
    }
}
