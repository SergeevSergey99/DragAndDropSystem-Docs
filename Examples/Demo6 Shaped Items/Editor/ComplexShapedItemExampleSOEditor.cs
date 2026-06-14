using UDND.Examples.ShapedItems;
using UnityEditor;
using UnityEngine;

namespace UDND.Examples.ShapedItems.Editor
{
    /// <summary>
    /// Inspector for <see cref="ComplexShapedItemExampleSO"/>: a clickable Width x Height grid that
    /// toggles occupied cells, drawn over the item's icon so the footprint can be authored to match
    /// the artwork. The grid maps 1:1 to the serialized row-major mask (top-left cell = index 0).
    /// </summary>
    [CustomEditor(typeof(ComplexShapedItemExampleSO))]
    public sealed class ComplexShapedItemExampleSOEditor : UnityEditor.Editor
    {
        private const float CellSize = 48f;
        private const float CellGap = 2f;

        private static readonly Color OccupiedTint = new Color(1f, 1f, 1f, 1f);
        private static readonly Color EmptyTint = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color GridLineColor = new Color(0f, 0f, 0f, 0.6f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var itemNameProp = serializedObject.FindProperty("_itemName");
            var iconProp = serializedObject.FindProperty("_icon");
            var widthProp = serializedObject.FindProperty("_width");
            var heightProp = serializedObject.FindProperty("_height");
            var cellsProp = serializedObject.FindProperty("_cells");

            EditorGUILayout.PropertyField(itemNameProp);
            EditorGUILayout.PropertyField(iconProp);
            EditorGUILayout.PropertyField(widthProp);
            EditorGUILayout.PropertyField(heightProp);

            int width = Mathf.Max(1, widthProp.intValue);
            int height = Mathf.Max(1, heightProp.intValue);
            EnsureMaskSize(cellsProp, width, height);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Footprint (click cells to toggle)", EditorStyles.boldLabel);

            DrawGrid(width, height, cellsProp, iconProp.objectReferenceValue as Sprite);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGrid(int width, int height, SerializedProperty cellsProp, Sprite icon)
        {
            float totalWidth = width * CellSize + (width - 1) * CellGap;
            float totalHeight = height * CellSize + (height - 1) * CellGap;

            Rect gridRect = GUILayoutUtility.GetRect(totalWidth, totalHeight, GUILayout.ExpandWidth(false));

            // Icon spans the whole bounding box so painted cells line up with the artwork.
            if (icon != null)
                DrawSprite(gridRect, icon);

            Event evt = Event.current;

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int index = row * width + col;
                    if (index >= cellsProp.arraySize)
                        continue;

                    SerializedProperty cell = cellsProp.GetArrayElementAtIndex(index);
                    Rect cellRect = new Rect(
                        gridRect.x + col * (CellSize + CellGap),
                        gridRect.y + row * (CellSize + CellGap),
                        CellSize,
                        CellSize);

                    if (evt.type == EventType.MouseDown && evt.button == 0 && cellRect.Contains(evt.mousePosition))
                    {
                        cell.boolValue = !cell.boolValue;
                        evt.Use();
                    }

                    if (evt.type == EventType.Repaint)
                    {
                        if (!cell.boolValue)
                            EditorGUI.DrawRect(cellRect, EmptyTint);

                        DrawBorder(cellRect, GridLineColor);
                    }
                }
            }
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            Texture texture = sprite.texture;
            if (texture == null)
                return;

            Rect spriteRect = sprite.rect;
            Rect uv = new Rect(
                spriteRect.x / texture.width,
                spriteRect.y / texture.height,
                spriteRect.width / texture.width,
                spriteRect.height / texture.height);

            GUI.color = OccupiedTint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        // Keeps the serialized mask sized to Width * Height, defaulting newly exposed cells to
        // occupied so a freshly authored item starts as a full rectangle.
        private static void EnsureMaskSize(SerializedProperty cellsProp, int width, int height)
        {
            int expected = width * height;
            if (cellsProp.arraySize == expected)
                return;

            int previous = cellsProp.arraySize;
            cellsProp.arraySize = expected;
            for (int i = previous; i < expected; i++)
                cellsProp.GetArrayElementAtIndex(i).boolValue = true;
        }
    }
}
