using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря экипировки игрока
    /// Синхронизирует UI слотов экипировки с полями экипировки в PlayerData
    ///
    /// ПРИМЕР: Демонстрирует использование проверки по типу предмета и работу с фиксированными слотами
    /// </summary>
    public class EquipmentInventoryDataBinding : TradingInventoryDataBinding
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

        /// <summary>
        /// Проверка возможности сбросить предмет в слот экипировки
        /// Проверяем соответствие типа предмета типу слота
        /// </summary>
        protected override RuleResult CanDropInternal(DragContext context, DragEntry entry)
        {
            // Если это программное добавление (SyncToUI) - разрешаем
            if (IsProgrammaticOperation(context, entry))
            {
                return RuleResult.Success();
            }

            // Проверяем что предмет торговый
            if (entry.Stack.Item is not ITradableItem tradable)
            {
                return RuleResult.Failure("Неверный тип предмета");
            }

            // Проверяем покупку у торговца (если применимо)
            var purchaseResult = ValidatePurchaseFromMerchant(context, entry);
            if (purchaseResult.HasValue && !purchaseResult.Value.IsValid)
            {
                return purchaseResult.Value;
            }

            // Определяем тип предмета для проверки соответствия слоту
            ItemType itemType = tradable.ItemType;
            var targetSlot = context.TargetSlot;

            // Проверяем соответствие типа предмета слоту
            if (targetSlot == _weaponSlot)
            {
                return itemType == ItemType.Weapon
                    ? RuleResult.Success()
                    : RuleResult.Failure($"В этот слот можно положить только оружие");
            }

            if (targetSlot == _armorSlot)
            {
                return itemType == ItemType.Armor
                    ? RuleResult.Success()
                    : RuleResult.Failure($"В этот слот можно положить только броню");
            }

            if (targetSlot == _artifact1Slot || targetSlot == _artifact2Slot)
            {
                return itemType == ItemType.Artifact
                    ? RuleResult.Success()
                    : RuleResult.Failure($"В этот слот можно положить только артефакты");
            }

            return RuleResult.Failure("Неизвестный слот экипировки");
        }

        /// <summary>
        /// Проверка возможности начать перетаскивание из слота экипировки
        /// Разрешаем снимать экипировку
        /// </summary>
        protected override RuleResult CanStartDragInternal(DragContext context, DragEntry entry)
        {
            return RuleResult.Success();
        }

        /// <summary>
        /// Обработка экипировки предмета
        /// Когда предмет добавляется в слот экипировки - экипируем его в PlayerData
        /// </summary>
        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Item is not ITradableItem tradable)
            {
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Cannot equip - item is not ITradableItem");
                return;
            }

            // Если предмет пришел от торговца - покупаем у него
            TryHandlePurchaseFromMerchant(context);

            var slot = context.TargetSlot;
            TradableItemModel item;

            // Получаем модель предмета или конвертируем из SO
            if (context.Item is TradableItemModelAdapter modelAdapter)
            {
                item = modelAdapter.Item;
            }
            else
            {
                item = ConvertToModelAdapter(context.TargetSlot, tradable);
            }

            // Экипируем предмет в соответствующий слот
            if (slot == _weaponSlot)
            {
                PlayerData.EquipWeapon(item);
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Equipped weapon: {item.originalSO.DisplayName}");
            }
            else if (slot == _armorSlot)
            {
                PlayerData.EquipArmor(item);
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Equipped armor: {item.originalSO.DisplayName}");
            }
            else if (slot == _artifact1Slot)
            {
                PlayerData.EquipArtifact1(item);
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Equipped artifact 1: {item.originalSO.DisplayName}");
            }
            else if (slot == _artifact2Slot)
            {
                PlayerData.EquipArtifact2(item);
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Equipped artifact 2: {item.originalSO.DisplayName}");
            }
        }

        /// <summary>
        /// Обработка снятия экипировки
        /// Когда предмет удаляется из слота экипировки - снимаем его в PlayerData
        /// </summary>
        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            // Если предмет ушел к торговцу - продаем ему
            TryHandleSellToMerchant(context);

            // Снимаем экипировку из соответствующего слота
            var slot = context.SourceSlot;
            if (ReferenceEquals(slot, _weaponSlot))
            {
                PlayerData.UnequipWeapon();
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Unequipped weapon: {context.Item.DisplayName}");
            }
            else if (ReferenceEquals(slot, _armorSlot))
            {
                PlayerData.UnequipArmor();
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Unequipped armor: {context.Item.DisplayName}");
            }
            else if (ReferenceEquals(slot, _artifact1Slot))
            {
                PlayerData.UnequipArtifact1();
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Unequipped artifact 1: {context.Item.DisplayName}");
            }
            else if (ReferenceEquals(slot, _artifact2Slot))
            {
                PlayerData.UnequipArtifact2();
                Extentions.DragAndDropLog($"[EquipmentInventoryDataBinding] Unequipped artifact 2: {context.Item.DisplayName}");
            }
        }

        /// <summary>
        /// Синхронизация UI с данными экипировки из PlayerData
        /// </summary>
        public override void ReloadUI()
        {
            if (_inventory == null || PlayerData == null) return;

            using (BeginSync())
            {
                _inventory.ClearAll();

                // Синхронизируем оружие
                if (PlayerData.EquippedWeapon != null)
                {
                    var adapter = new TradableItemModelAdapter(PlayerData.EquippedWeapon);
                    AddToUIQuiet(adapter, 1, _weaponSlot.Index);
                }

                // Синхронизируем броню
                if (PlayerData.EquippedArmor != null)
                {
                    var adapter = new TradableItemModelAdapter(PlayerData.EquippedArmor);
                    AddToUIQuiet(adapter, 1, _armorSlot.Index);
                }

                // Синхронизируем артефакт 1
                if (PlayerData.EquippedArtifact1 != null)
                {
                    var adapter = new TradableItemModelAdapter(PlayerData.EquippedArtifact1);
                    AddToUIQuiet(adapter, 1, _artifact1Slot.Index);
                }

                // Синхронизируем артефакт 2
                if (PlayerData.EquippedArtifact2 != null)
                {
                    var adapter = new TradableItemModelAdapter(PlayerData.EquippedArtifact2);
                    AddToUIQuiet(adapter, 1, _artifact2Slot.Index);
                }
            }
        }

        #region Unity Editor Validation

        private void OnValidate()
        {
            // Проверяем что все слоты назначены
            if (_weaponSlot == null)
                Debug.LogWarning($"[{GetType().Name}] Weapon slot is not assigned!", this);

            if (_armorSlot == null)
                Debug.LogWarning($"[{GetType().Name}] Armor slot is not assigned!", this);

            if (_artifact1Slot == null)
                Debug.LogWarning($"[{GetType().Name}] Artifact 1 slot is not assigned!", this);

            if (_artifact2Slot == null)
                Debug.LogWarning($"[{GetType().Name}] Artifact 2 slot is not assigned!", this);

            // Проверяем что слоты уникальные
            if (_weaponSlot != null && _armorSlot != null && _weaponSlot == _armorSlot)
                Debug.LogError($"[{GetType().Name}] Weapon and Armor slots are the same!", this);

            if (_artifact1Slot != null && _artifact2Slot != null && _artifact1Slot == _artifact2Slot)
                Debug.LogError($"[{GetType().Name}] Artifact 1 and Artifact 2 slots are the same!", this);
        }

        #endregion
    }
}
