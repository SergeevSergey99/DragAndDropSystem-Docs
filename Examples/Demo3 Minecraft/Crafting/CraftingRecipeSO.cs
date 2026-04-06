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
        [Tooltip("3x3 pattern. Order: row by row, left to right. null = empty cell")]
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
        public bool Matches(MinecraftItemSO[] gridItemIds)
        {
            if (gridItemIds == null || gridItemIds.Length != 9 || _result == null)
                return false;

            return _shapeless ? MatchesShapeless(gridItemIds) : MatchesShaped(gridItemIds);
        }
        #region Shaped matching (с учётом смещения)

        private bool MatchesShaped(MinecraftItemSO[] gridItemIds)
        {
            var patternIds = GetPatternItems();

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
        private static void Normalize(MinecraftItemSO[] grid3x3, out MinecraftItemSO[] items, out int rows, out int cols)
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
                items = Array.Empty<MinecraftItemSO>();
                rows = 0;
                cols = 0;
                return;
            }

            rows = maxR - minR + 1;
            cols = maxC - minC + 1;
            items = new MinecraftItemSO[rows * cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    items[r * cols + c] = grid3x3[(minR + r) * 3 + (minC + c)];
        }

        #endregion

        #region Shapeless matching (порядок не важен)

        private bool MatchesShapeless(MinecraftItemSO[] gridItems)
        {
            var patternList = new List<MinecraftItemSO>();
            foreach (var item in GetPatternItems())
                if (item != null) patternList.Add(item);

            var gridList = new List<MinecraftItemSO>();
            foreach (var id in gridItems)
                if (id != null) gridList.Add(id);

            if (patternList.Count != gridList.Count)
                return false;

            patternList.Sort();
            gridList.Sort();

            for (int i = 0; i < patternList.Count; i++)
                if (patternList[i] != gridList[i])
                    return false;

            return true;
        }

        #endregion

        /// <summary>
        /// Сколько раз можно скрафтить этот рецепт с текущими ингредиентами.
        /// Вызывать после Matches() == true.
        /// </summary>
        public int ComputeMaxCrafts(MinecraftItemSO[] gridItems, int[] gridCounts)
        {
            if (gridItems == null || gridCounts == null || gridItems.Length != 9 || gridCounts.Length != 9)
                return 0;

            return _shapeless
                ? ComputeMaxCraftsShapeless(gridItems, gridCounts)
                : ComputeMaxCraftsShaped(gridItems, gridCounts);
        }

        private int ComputeMaxCraftsShaped(MinecraftItemSO[] gridItems, int[] gridCounts)
        {
            var patternItems = GetPatternItems();
            GetBoundingBox(patternItems, out int pMinR, out int pMinC);
            GetBoundingBox(gridItems, out int gMinR, out int gMinC);

            int offsetR = gMinR - pMinR;
            int offsetC = gMinC - pMinC;

            int maxCrafts = int.MaxValue;
            for (int i = 0; i < 9; i++)
            {
                if (patternItems[i] == null) continue;

                int r = i / 3 + offsetR;
                int c = i % 3 + offsetC;
                int gridIdx = r * 3 + c;

                if (gridIdx < 0 || gridIdx >= 9 || gridCounts[gridIdx] <= 0)
                    return 0;

                maxCrafts = Math.Min(maxCrafts, gridCounts[gridIdx]);
            }

            return maxCrafts == int.MaxValue ? 0 : maxCrafts;
        }

        private int ComputeMaxCraftsShapeless(MinecraftItemSO[] gridItems, int[] gridCounts)
        {
            // Собираем требования паттерна: сколько ячеек на каждый тип предмета
            var patternReq = new Dictionary<MinecraftItemSO, int>();
            var patternItems = GetPatternItems();
            for (int i = 0; i < 9; i++)
            {
                if (patternItems[i] == null) continue;
                if (!patternReq.ContainsKey(patternItems[i]))
                    patternReq[patternItems[i]] = 0;
                patternReq[patternItems[i]]++;
            }

            // Собираем доступное в гриде: суммарное количество каждого типа
            var gridAvail = new Dictionary<MinecraftItemSO, int>();
            for (int i = 0; i < 9; i++)
            {
                if (gridItems[i] == null || gridCounts[i] <= 0) continue;
                if (!gridAvail.ContainsKey(gridItems[i]))
                    gridAvail[gridItems[i]] = 0;
                gridAvail[gridItems[i]] += gridCounts[i];
            }

            int maxCrafts = int.MaxValue;
            foreach (var kvp in patternReq)
            {
                if (!gridAvail.TryGetValue(kvp.Key, out int available))
                    return 0;
                maxCrafts = Math.Min(maxCrafts, available / kvp.Value);
            }

            return maxCrafts == int.MaxValue ? 0 : maxCrafts;
        }

        /// <summary>
        /// Возвращает массив [9]: сколько единиц потреблять из каждого слота за ОДИН крафт.
        /// Вызывать только когда Matches(gridItems) == true.
        /// </summary>
        public int[] GetConsumeAmountsPerCraft(MinecraftItemSO[] gridItems)
        {
            return _shapeless
                ? GetConsumeAmountsShapeless(gridItems)
                : GetConsumeAmountsShaped(gridItems);
        }

        private int[] GetConsumeAmountsShaped(MinecraftItemSO[] gridItems)
        {
            var result = new int[9];
            var patternItems = GetPatternItems();
            GetBoundingBox(patternItems, out int pMinR, out int pMinC);
            GetBoundingBox(gridItems,    out int gMinR, out int gMinC);
            int offsetR = gMinR - pMinR;
            int offsetC = gMinC - pMinC;

            for (int i = 0; i < 9; i++)
            {
                if (patternItems[i] == null) continue;
                int r = i / 3 + offsetR;
                int c = i % 3 + offsetC;
                int gridIdx = r * 3 + c;
                if (gridIdx >= 0 && gridIdx < 9)
                    result[gridIdx] = 1;
            }
            return result;
        }

        private static int[] GetConsumeAmountsShapeless(MinecraftItemSO[] gridItems)
        {
            var result = new int[9];
            for (int i = 0; i < 9; i++)
                if (gridItems[i] != null)
                    result[i] = 1;
            return result;
        }

        private static void GetBoundingBox(MinecraftItemSO[] grid3x3, out int minR, out int minC)
        {
            minR = 3;
            minC = 3;
            for (int i = 0; i < 9; i++)
            {
                if (grid3x3[i] != null)
                {
                    int r = i / 3, c = i % 3;
                    if (r < minR) minR = r;
                    if (c < minC) minC = c;
                }
            }
        }

        private MinecraftItemSO[] GetPatternItems()
        {
            var ids = new MinecraftItemSO[9];
            for (int i = 0; i < 9; i++)
                ids[i] = _pattern.Get(i) != null ? _pattern.Get(i) : null;
            return ids;
        }
    }
}
