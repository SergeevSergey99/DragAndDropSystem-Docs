using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// MappedSlot binding для экипировки (4 слота: шлем, нагрудник, поножи, ботинки).
    /// Каждый слот принимает только предметы с соответствующим EquipmentSlotType.
    /// </summary>
    public class EquipmentBinding : MappedSlotInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        [Header("Data")]
        [SerializeField] private MinecraftItemDatabase _database;
        [SerializeField] private SlotSaveData[] _saveSlots = new SlotSaveData[4];

        [Header("Slot References")]
        [SerializeField] private UniversalSlot _helmetSlot;
        [SerializeField] private UniversalSlot _chestSlot;
        [SerializeField] private UniversalSlot _legsSlot;
        [SerializeField] private UniversalSlot _bootsSlot;

        protected override Dictionary<ISlot, SlotBinding<MinecraftItemSO>> CreateBindingMap() => new()
        {
            [_helmetSlot] = CreateSlotBinding(0, EquipmentSlotType.Helmet),
            [_chestSlot] = CreateSlotBinding(1, EquipmentSlotType.Chest),
            [_legsSlot] = CreateSlotBinding(2, EquipmentSlotType.Legs),
            [_bootsSlot] = CreateSlotBinding(3, EquipmentSlotType.Boots),
        };

        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        protected override MinecraftItemSO ExtractData(MinecraftItemAdapter adapter) => adapter.ItemSO;

        private SlotBinding<MinecraftItemSO> CreateSlotBinding(int index, EquipmentSlotType slotType)
        {
            return new SlotBinding<MinecraftItemSO>(
                get: () =>
                {
                    var data = _saveSlots[index];
                    return data != null && !string.IsNullOrEmpty(data.ItemId)
                        ? _database.GetItem(data.ItemId)
                        : null;
                },
                set: item =>
                {
                    _saveSlots[index] = new SlotSaveData { ItemId = item.ItemId, Count = 1 };
                },
                clear: () =>
                {
                    _saveSlots[index] = null;
                },
                canAccept: item =>
                {
                    if (item.EquipmentSlot == slotType)
                        return RuleResult.Success();
                    return RuleResult.Failure($"Only {slotType} can be equipped here");
                }
            );
        }
    }
}
