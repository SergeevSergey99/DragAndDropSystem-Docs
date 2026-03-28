using System;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    [Serializable]
    public class CraftingRecipePattern : ISerializationCallbackReceiver
    {
        public const int Size = 3;
        public const int CellCount = Size * Size;

        [SerializeField] private MinecraftItemSO[] _cells = new MinecraftItemSO[CellCount];

        public MinecraftItemSO Get(int index)
        {
            EnsureCapacity();
            return index >= 0 && index < CellCount ? _cells[index] : null;
        }

        public MinecraftItemSO Get(int row, int column)
        {
            return Get(row * Size + column);
        }

        public void Set(int index, MinecraftItemSO item)
        {
            EnsureCapacity();
            if (index < 0 || index >= CellCount)
                return;

            _cells[index] = item;
        }

        public void OnBeforeSerialize()
        {
            EnsureCapacity();
        }

        public void OnAfterDeserialize()
        {
            EnsureCapacity();
        }

        private void EnsureCapacity()
        {
            if (_cells == null || _cells.Length != CellCount)
            {
                Array.Resize(ref _cells, CellCount);
            }
        }
    }
}
