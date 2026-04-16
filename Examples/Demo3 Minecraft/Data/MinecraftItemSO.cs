using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Examples.Minecraft
{
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Minecraft/MinecraftItemSO")]
    public class MinecraftItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(120)] public Sprite Icon { get; private set; }
    }
}
