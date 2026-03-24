using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// Универсальный SlotIndexed binding для хотбара, основного инвентаря и сундуков.
    /// Хранит данные в массиве SlotSaveData[] — каждый элемент = один слот.
    /// Для сохранения на диск сериализуйте _saveSlots (например, в JSON).
    /// </summary>
    public class MinecraftSlotBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        [Header("Data")]
        [SerializeField, Tooltip("База предметов для поиска по ItemId при загрузке")]
        private MinecraftItemDatabase _database;

        [SerializeField, Tooltip("Данные слотов. Размер массива = количество слотов инвентаря")]
        private SlotSaveData[] _saveSlots;

        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            if (_saveSlots == null)
                yield break;

            for (int i = 0; i < _saveSlots.Length; i++)
            {
                var slot = _saveSlots[i];
                if (slot == null || string.IsNullOrEmpty(slot.ItemId))
                    continue;

                var item = _database.GetItem(slot.ItemId);
                if (item != null && slot.Count > 0)
                    yield return (i, item, slot.Count);
            }
        }

        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        protected override MinecraftItemSO ExtractData(MinecraftItemAdapter adapter) => adapter.ItemSO;

        protected override void AddToSlotData(int index, MinecraftItemSO item, int count)
        {
            EnsureCapacity(index + 1);

            if (_saveSlots[index] != null && _saveSlots[index].ItemId == item.ItemId)
            {
                _saveSlots[index].Count += count;
            }
            else
            {
                _saveSlots[index] = new SlotSaveData { ItemId = item.ItemId, Count = count };
            }
        }

        protected override void RemoveFromSlotData(int index, MinecraftItemSO item, int count)
        {
            if (_saveSlots == null || index < 0 || index >= _saveSlots.Length || _saveSlots[index] == null)
                return;

            _saveSlots[index].Count -= count;
            if (_saveSlots[index].Count <= 0)
                _saveSlots[index] = null;
        }

        private void EnsureCapacity(int minSize)
        {
            if (_saveSlots == null)
                _saveSlots = new SlotSaveData[minSize];
            else if (_saveSlots.Length < minSize)
                Array.Resize(ref _saveSlots, minSize);
        }
    }
}
