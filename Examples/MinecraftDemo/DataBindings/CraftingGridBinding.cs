using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// Binding для сетки крафта 3x3. Хранит предметы только в памяти (без сохранения).
    /// При любом изменении содержимого вызывает OnGridChanged — CraftingManager подписывается
    /// и проверяет рецепт.
    /// </summary>
    public class CraftingGridBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        /// <summary>
        /// Вызывается при любом добавлении/удалении предмета из сетки.
        /// </summary>
        public event Action OnGridChanged;

        private readonly SlotSaveData[] _gridSlots = new SlotSaveData[9];

        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            // In-memory only — при ReloadUI просто пустая сетка
            yield break;
        }

        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        protected override MinecraftItemSO ExtractData(MinecraftItemAdapter adapter) => adapter.ItemSO;

        protected override void AddToSlotData(int index, MinecraftItemSO item, int count)
        {
            if (index < 0 || index >= _gridSlots.Length) return;

            if (_gridSlots[index] != null && _gridSlots[index].ItemId == item.ItemId)
                _gridSlots[index].Count += count;
            else
                _gridSlots[index] = new SlotSaveData { ItemId = item.ItemId, Count = count };

            OnGridChanged?.Invoke();
        }

        protected override void RemoveFromSlotData(int index, MinecraftItemSO item, int count)
        {
            if (index < 0 || index >= _gridSlots.Length || _gridSlots[index] == null) return;

            _gridSlots[index].Count -= count;
            if (_gridSlots[index].Count <= 0)
                _gridSlots[index] = null;

            OnGridChanged?.Invoke();
        }
    }
}
