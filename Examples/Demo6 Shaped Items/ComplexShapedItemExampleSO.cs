using System.Collections.Generic;
using UnityEngine;

namespace UDND.Examples.ShapedItems
{
    /// <summary>
    /// Shaped item example with a non-rectangular footprint authored as a cell mask
    /// (L / T / cross / etc). The bounding box is <c>Width x Height</c>; each cell in the
    /// row-major mask (index = y * Width + x) toggles whether that cell is occupied.
    /// Like the base type it stays free of inventory/placement system types.
    /// </summary>
    [CreateAssetMenu(fileName = "ComplexShapedItemExampleSO", menuName = "DragAndDrop/Examples/Complex Shaped Item", order = 7)]
    public class ComplexShapedItemExampleSO : ShapedItemExampleSO
    {
        [SerializeField] private bool[] _cells;

        public bool GetCell(int x, int y)
        {
            int index = CellIndex(x, y);
            // Unset mask cells default to occupied so a freshly authored item is a full rectangle.
            return _cells == null || index < 0 || index >= _cells.Length || _cells[index];
        }

        public override IReadOnlyList<Vector2Int> GetOccupiedCells()
        {
            var result = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (GetCell(x, y))
                        result.Add(new Vector2Int(x, y));
                }
            }

            // An empty mask falls back to the base rectangle so the item is never footprint-less.
            return result.Count > 0 ? result : base.GetOccupiedCells();
        }

        private int CellIndex(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return -1;

            return y * Width + x;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureMaskSize();
        }

        // Resizes the mask to Width * Height, preserving overlapping cells and defaulting
        // newly exposed cells to occupied (keeps the default shape a full rectangle).
        private void EnsureMaskSize()
        {
            int expected = Width * Height;
            if (_cells != null && _cells.Length == expected)
                return;

            int oldWidth = _cells != null && Height > 0 ? Mathf.Max(1, _cells.Length / Mathf.Max(1, Height)) : 0;
            int oldHeight = oldWidth > 0 ? _cells.Length / oldWidth : 0;

            var resized = new bool[expected];
            for (int i = 0; i < expected; i++)
                resized[i] = true;

            if (_cells != null && oldWidth > 0)
            {
                for (int y = 0; y < Mathf.Min(Height, oldHeight); y++)
                {
                    for (int x = 0; x < Mathf.Min(Width, oldWidth); x++)
                    {
                        int oldIndex = y * oldWidth + x;
                        if (oldIndex < _cells.Length)
                            resized[y * Width + x] = _cells[oldIndex];
                    }
                }
            }

            _cells = resized;
        }
    }
}
