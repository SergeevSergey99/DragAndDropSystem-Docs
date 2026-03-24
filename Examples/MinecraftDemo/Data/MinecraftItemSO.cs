using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Minecraft/Item")]
    public class MinecraftItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(120)] public Sprite Icon { get; private set; }

        [field: SerializeField]
        public EquipmentSlotType EquipmentSlot { get; private set; } = EquipmentSlotType.None;
    }
}
