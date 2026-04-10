using DragAndDropSystem.Examples.Minecraft;
using DragAndDropSystem.Tools.Inspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples.Demo3_Minecraft.Editor
{
    
    [CustomPropertyDrawer(typeof(CraftingRecipePattern))]
    public sealed class CraftingRecipePatternPropertyDrawer : PropertyDrawer
    {
        private const float CellSize = 72f;
        private const float CellFooterHeight = 18f;
        private const float CellPadding = 4f;
        private const int GridSize = 3;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty cellsProperty = property.FindPropertyRelative("_cells");
            EnsureArraySize(cellsProperty, CraftingRecipePattern.CellCount);

            Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            float y = labelRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            float totalCellHeight = CellSize + CellFooterHeight;

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    int index = row * GridSize + col;
                    SerializedProperty element = cellsProperty.GetArrayElementAtIndex(index);

                    float x = position.x + col * (CellSize + EditorGUIUtility.standardVerticalSpacing);
                    Rect cellRect = new Rect(x, y, CellSize, totalCellHeight);

                    if (Event.current.type == EventType.ContextClick && cellRect.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                    }

                    GUI.Box(cellRect, GUIContent.none, EditorStyles.helpBox);

                    Rect previewRect = new Rect(
                        cellRect.x + CellPadding,
                        cellRect.y + CellPadding,
                        cellRect.width - CellPadding * 2f,
                        CellSize - CellPadding * 2f);
                    DrawCellPreview(previewRect, element.objectReferenceValue);

                    Rect fieldRect = new Rect(
                        cellRect.x + 1f,
                        cellRect.yMax - CellFooterHeight,
                        cellRect.width - 2f,
                        CellFooterHeight);

                    UnityEngine.Object newValue = EditorGUI.ObjectField(
                        fieldRect,
                        GUIContent.none,
                        element.objectReferenceValue,
                        typeof(MinecraftItemSO),
                        false);
                    if (newValue != element.objectReferenceValue)
                    {
                        element.objectReferenceValue = newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }

                y += totalCellHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalCellHeight = CellSize + CellFooterHeight;
            return EditorGUIUtility.singleLineHeight
                   + EditorGUIUtility.standardVerticalSpacing
                   + GridSize * totalCellHeight
                   + (GridSize - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        private static void EnsureArraySize(SerializedProperty property, int expectedSize)
        {
            if (property == null || !property.isArray || property.propertyType == SerializedPropertyType.String)
                return;

            if (property.arraySize == expectedSize)
                return;

            property.arraySize = expectedSize;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DrawCellPreview(Rect previewRect, UnityEngine.Object target)
        {
            GUI.Box(previewRect, GUIContent.none, EditorStyles.objectFieldThumb);
            if (target == null)
                return;

            Texture texture = InspectorPreviewUtility.GetPreviewTexture(target);
            if (texture == null)
                return;

            Rect contentRect = new Rect(
                previewRect.x + 2f,
                previewRect.y + 2f,
                previewRect.width - 4f,
                previewRect.height - 4f);

            if (texture is Texture2D texture2D && InspectorPreviewUtility.TryGetPreviewSprite(target, out Sprite sprite))
            {
                Rect fittedRect = InspectorPreviewUtility.GetAspectFitRect(contentRect, sprite.rect.width, sprite.rect.height);
                Rect uv = new Rect(
                    sprite.rect.x / texture2D.width,
                    sprite.rect.y / texture2D.height,
                    sprite.rect.width / texture2D.width,
                    sprite.rect.height / texture2D.height);
                GUI.DrawTextureWithTexCoords(fittedRect, texture, uv, true);
                return;
            }

            Rect textureRect = InspectorPreviewUtility.GetAspectFitRect(contentRect, texture.width, texture.height);
            GUI.DrawTexture(textureRect, texture, ScaleMode.StretchToFill, true);
        }
    }
}