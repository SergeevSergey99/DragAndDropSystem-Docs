using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Minecraft/Item")]
    public class MinecraftItemSO : ScriptableObject
    {
        [field: SerializeField] public string ItemId { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }

        [field: SerializeField, Range(1, 64)]
        public int MaxStackSize { get; private set; } = 64;

        [field: SerializeField]
        public EquipmentSlotType EquipmentSlot { get; private set; } = EquipmentSlotType.None;
    }
}
