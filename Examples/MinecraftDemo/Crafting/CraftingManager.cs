using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class CraftingManager : MonoBehaviour
    {
        [SerializeField] private MinecraftItemSO[] _hotbarItems = new MinecraftItemSO[9];
        [SerializeField] private MinecraftItemSO[] _inventoryItems = new MinecraftItemSO[27];

        [SerializeField] private CraftingRecipeSO[] _recipes;



    }
}
