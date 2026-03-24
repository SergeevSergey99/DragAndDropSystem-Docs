using System;
using System.Collections.Generic;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// Рецепт крафта. Поддерживает shaped (с учётом позиции, со смещением) и shapeless (порядок не важен).
    /// Паттерн 3x3: индекс = row * 3 + col. null = пустая ячейка.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Minecraft/Crafting Recipe")]
    public class CraftingRecipeSO : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Паттерн 3x3. Порядок: строка за строкой, слева направо. null = пустая ячейка")]
        private CraftingRecipePattern _pattern = new CraftingRecipePattern();

        [SerializeField] private bool _shapeless;

        [SerializeField, PreviewField(72f)] private MinecraftItemSO _result;
        [SerializeField, Range(1, 64)] private int _resultCount = 1;

        public MinecraftItemSO Result => _result;
        public int ResultCount => _resultCount;

        /// <summary>
        /// Проверить, совпадает ли содержимое сетки с рецептом.
        /// gridItemIds — массив из 9 ItemId (null = пустой слот).
        /// </summary>
        public bool Matches(string[] gridItemIds)
        {
            if (gridItemIds == null || gridItemIds.Length != 9 || _result == null)
                return false;

            return _shapeless ? MatchesShapeless(gridItemIds) : MatchesShaped(gridItemIds);
        }

        /// <summary>
        /// Удобный метод: прочитать ItemId прямо из слотов инвентаря.
        /// </summary>
        public bool Matches(IReadOnlyList<ISlot> gridSlots)
        {
            if (gridSlots == null || gridSlots.Count != 9)
                return false;

            var ids = new string[9];
            for (int i = 0; i < 9; i++)
                ids[i] = gridSlots[i].IsEmpty ? null : gridSlots[i].Stack.Item.ItemId;

            return Matches(ids);
        }

        #region Shaped matching (с учётом смещения)

        private bool MatchesShaped(string[] gridItemIds)
        {
            var patternIds = GetPatternItemIds();

            Normalize(patternIds, out var pItems, out int pRows, out int pCols);
            Normalize(gridItemIds, out var gItems, out int gRows, out int gCols);

            if (pRows != gRows || pCols != gCols)
                return false;

            if (pItems.Length != gItems.Length)
                return false;

            for (int i = 0; i < pItems.Length; i++)
            {
                if (pItems[i] != gItems[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Убрать пустые строки/столбцы с краёв и вернуть минимальный прямоугольник.
        /// </summary>
        private static void Normalize(string[] grid3x3, out string[] items, out int rows, out int cols)
        {
            int minR = 3, maxR = -1, minC = 3, maxC = -1;
            for (int i = 0; i < 9; i++)
            {
                if (grid3x3[i] != null)
                {
                    int r = i / 3, c = i % 3;
                    if (r < minR) minR = r;
                    if (r > maxR) maxR = r;
                    if (c < minC) minC = c;
                    if (c > maxC) maxC = c;
                }
            }

            if (maxR < 0)
            {
                items = Array.Empty<string>();
                rows = 0;
                cols = 0;
                return;
            }

            rows = maxR - minR + 1;
            cols = maxC - minC + 1;
            items = new string[rows * cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    items[r * cols + c] = grid3x3[(minR + r) * 3 + (minC + c)];
        }

        #endregion

        #region Shapeless matching (порядок не важен)

        private bool MatchesShapeless(string[] gridItemIds)
        {
            var patternList = new List<string>();
            foreach (var id in GetPatternItemIds())
                if (id != null) patternList.Add(id);

            var gridList = new List<string>();
            foreach (var id in gridItemIds)
                if (id != null) gridList.Add(id);

            if (patternList.Count != gridList.Count)
                return false;

            patternList.Sort(StringComparer.Ordinal);
            gridList.Sort(StringComparer.Ordinal);

            for (int i = 0; i < patternList.Count; i++)
                if (patternList[i] != gridList[i])
                    return false;

            return true;
        }

        #endregion

        private string[] GetPatternItemIds()
        {
            var ids = new string[9];
            for (int i = 0; i < 9; i++)
                ids[i] = _pattern.Get(i) != null ? _pattern.Get(i).ItemId : null;
            return ids;
        }
    }
}
