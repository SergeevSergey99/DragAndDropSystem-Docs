using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
{
    /// <summary>
    /// DataBinding для инвентаря игрока.
    /// Связывает PlayerInventoryData (данные) ↔ UniversalInventory (UI).
    /// Сохраняет позиции предметов в слотах.
    /// </summary>
    public class PlayerInventoryDataBinding : SlotIndexedInventoryDataBinding<ItemExampleWith3DSO, ItemSOWith3DAdapter>
    {
        [Header("Player Data")]
        [SerializeField, Tooltip("Компонент с данными инвентаря игрока")]
        private PlayerInventoryData _playerData;

        protected override IEnumerable<(int index, ItemExampleWith3DSO item, int count)> GetOccupiedSlots()
        {
            var slots = _playerData.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null)
                    yield return (i, slots[i], 1);
        }

        protected override ItemSOWith3DAdapter CreateAdapter(ItemExampleWith3DSO item) => new(item);
        protected override void AddToSlotData(int index, ItemSOWith3DAdapter adapter, int count) => _playerData.SetItem(index, adapter.item);
        protected override void RemoveFromSlotData(int index, ItemSOWith3DAdapter adapter, int count) => _playerData.ClearSlot(index);
        
        // Дополнительно запрещаем класть в слот в любом режиме инвентаря
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Получаем индекс слота, на который пытаются сбросить предмет
            int targetIndex = context.TargetSlot.Index;

            // Проверяем, занят ли этот слот
            if (_playerData.GetItem(targetIndex) != null)
            {
                return RuleResult.Failure("Этот слот уже занят!");
            }

            // Если слот свободен, разрешаем сброс
            return RuleResult.Success();
        }
    }
}
