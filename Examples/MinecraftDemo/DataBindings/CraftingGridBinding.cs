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

        private readonly MinecraftItemSO[] _gridItems = new MinecraftItemSO[9];
        private readonly int[] _gridCounts = new int[9];

        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < _gridItems.Length; i++)
            {
                if (_gridItems[i] != null && _gridCounts[i] > 0)
                    yield return (i, _gridItems[i], _gridCounts[i]);
            }
        }

        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        protected override MinecraftItemSO ExtractData(MinecraftItemAdapter adapter) => adapter.ItemSO;

        protected override void AddToSlotData(int index, MinecraftItemSO item, int count)
        {
            if (index < 0 || index >= _gridItems.Length || item == null || count <= 0)
                return;

            if (_gridItems[index] != null && _gridItems[index] == item)
                _gridCounts[index] += count;
            else
            {
                _gridItems[index] = item;
                _gridCounts[index] = count;
            }

            OnGridChanged?.Invoke();
        }

        protected override void RemoveFromSlotData(int index, MinecraftItemSO item, int count)
        {
            if (index < 0 || index >= _gridItems.Length || _gridItems[index] == null || count <= 0)
                return;

            _gridCounts[index] -= count;
            if (_gridCounts[index] <= 0)
            {
                _gridItems[index] = null;
                _gridCounts[index] = 0;
            }

            OnGridChanged?.Invoke();
        }
    }
}
