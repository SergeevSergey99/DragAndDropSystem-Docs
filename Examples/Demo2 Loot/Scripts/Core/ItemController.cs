using UnityEngine;

namespace DragAndDropSystem.Examples.Loot
{
    [RequireComponent(typeof(WorldItem))]
    public class ItemController : MonoBehaviour, IInteractable
    {
        [field: SerializeField] public WorldItem WorldItem { get; private set; }
        [field: SerializeField] public SpriteRenderer spriteRenderer { get; private set; }
        
        public bool CanInteract(PlayerInteraction player)
        {
            return player.Inventory.IsFull == false;
        }
        public void Interact(PlayerInteraction player)
        {
            if (CanInteract(player))
            {
                if (player.Inventory.AddItem(WorldItem.Item))
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnValidate()
        {
            if (WorldItem == null)
                WorldItem = GetComponent<WorldItem>();
        }
    }
}