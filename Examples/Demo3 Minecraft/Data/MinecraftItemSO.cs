using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Minecraft/ItemAdapter")]
    public class MinecraftItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(120)] public Sprite Icon { get; private set; }
    }
}
