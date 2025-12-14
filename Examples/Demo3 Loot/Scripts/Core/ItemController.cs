using System;
using DragAndDropSystem.World3D;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo3Loot
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
            if (CanInteract(player) && WorldItem.itemData is ItemSOWith3DAdapter adapter)
            {
                if (player.Inventory.AddItem(adapter.item))
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