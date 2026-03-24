using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class CraftingManager : MonoSingleton<CraftingManager>
    {
        [SerializeField, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _hotbarItems = new RuntimeItem[9];
        [SerializeField, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _inventoryItems = new RuntimeItem[27];
        [SerializeField, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _craftTableItems = new RuntimeItem[9];

        [SerializeField] private List<CraftingRecipeSO> _recipes;

        public const int MaxItemsPerSlot = 64;
        
    }
}
