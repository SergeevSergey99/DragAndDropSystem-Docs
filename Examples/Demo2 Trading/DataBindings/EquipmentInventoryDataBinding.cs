using System.Collections.Generic;
using DragAndDropSystem.Core;
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
    public class EquipmentInventoryDataBinding : MappedSlotInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>, ITransferDomainHandler
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

        private PlayerData PlayerData => TradingEconomyManager.Instance.PlayerData;
        protected override IInventoryItemConverter CreateItemConverter() => new ModelInventoryItemConverter();

        // --- MappedSlotInventoryDataBinding примитивы ---

        protected override Dictionary<ISlot, SlotBinding<TradableItemModel>> CreateBindingMap() => new()
        {
            [_weaponSlot] = new(
                get: () => PlayerData.EquippedWeapon,
                set: item => PlayerData.EquipWeapon(item),
                clear: () => PlayerData.UnequipWeapon(),
                canAccept: item => item.originalSO.ItemType == ItemType.Weapon
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только оружие")),

            [_armorSlot] = new(
                get: () => PlayerData.EquippedArmor,
                set: item => PlayerData.EquipArmor(item),
                clear: () => PlayerData.UnequipArmor(),
                canAccept: item => item.originalSO.ItemType == ItemType.Armor
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только броню")),

            [_artifact1Slot] = new(
                get: () => PlayerData.EquippedArtifact1,
                set: item => PlayerData.EquipArtifact1(item),
                clear: () => PlayerData.UnequipArtifact1(),
                canAccept: item => item.originalSO.ItemType == ItemType.Artifact
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только артефакты")),

            [_artifact2Slot] = new(
                get: () => PlayerData.EquippedArtifact2,
                set: item => PlayerData.EquipArtifact2(item),
                clear: () => PlayerData.UnequipArtifact2(),
                canAccept: item => item.originalSO.ItemType == ItemType.Artifact
                    ? RuleResult.Success()
                    : RuleResult.Failure("В этот слот можно положить только артефакты")),
        };

        protected override TradableItemModelAdapter CreateAdapter(TradableItemModel item) => new(item);
        protected override TradableItemModel ExtractData(TradableItemModelAdapter adapter) => adapter.Item;

        public RuleResult CanCommitTransfer(TransferDomainContext context) => TradingHelper.ValidatePlayerTransfer(context, PlayerData);

        public void OnTransferSucceeded(TransferDomainContext context) => TradingHelper.ApplyPlayerTransferEffects(context, PlayerData);

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.Item is not ITradableItem tradable)
                return RuleResult.Failure("Неверный тип предмета");

            // Проверяем соответствие типа предмета слоту через canAccept из BindingMap
            if (!TryGetTargetBinding(context?.TargetSlot, out var binding))
                return RuleResult.Failure("Неизвестный слот экипировки");
            
            if (binding.CanAccept?.Invoke(new TradableItemModel(tradable.OriginalSO)).IsValid == false)
                return RuleResult.Failure("Этот предмет нельзя положить в этот слот");
            
            return RuleResult.Success();
        }
    }
}
