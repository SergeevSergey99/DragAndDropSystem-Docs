using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
{
    public class ItemController : MonoBehaviour, IInteractable
    {
        [field: SerializeField] public SpriteRenderer spriteRenderer { get; private set; }
        
        public ItemExampleWith3DSO ItemExampleWith3DSO { get; private set; }
        
        public void Initialize(ItemExampleWith3DSO itemExampleWith3DSO)
        {
            ItemExampleWith3DSO = itemExampleWith3DSO;
        }
        public void Interact(PlayerInteraction player)
        {
            Debug.Log($"Player {player.name} interacted with item {name}");
        }
    }
}