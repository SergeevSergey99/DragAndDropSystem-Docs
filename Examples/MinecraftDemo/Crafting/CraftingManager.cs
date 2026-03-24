using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class CraftingManager : MonoSingleton<CraftingManager>
    {
        [SerializeReference, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _hotbarItems = new RuntimeItem[9];
        [SerializeReference, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _inventoryItems = new RuntimeItem[27];
        [SerializeReference, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _craftTableItems = new RuntimeItem[9];

        [SerializeField] private List<CraftingRecipeSO> _recipes;

        public const int MaxItemsPerSlot = 64;
        
        public IReadOnlyList<RuntimeItem> HotbarItems => _hotbarItems;
        public IReadOnlyList<RuntimeItem> InventoryItems => _inventoryItems;
        public IReadOnlyList<RuntimeItem> CraftTableItems => _craftTableItems;
        
        public bool CanAddHotbarItem(MinecraftItemSO item, int count, int index) =>
            _hotbarItems[index] == null || (_hotbarItems[index].ItemSO == item && _hotbarItems[index].Count + count <= MaxItemsPerSlot);
        public bool CanAddInventoryItem(MinecraftItemSO item, int count, int index) =>
            _inventoryItems[index] == null || (_inventoryItems[index].ItemSO == item && _inventoryItems[index].Count + count <= MaxItemsPerSlot);
        public bool CanAddCraftTableItem(MinecraftItemSO item, int count, int index) =>
            _craftTableItems[index] == null || (_craftTableItems[index].ItemSO == item && _craftTableItems[index].Count + count <= MaxItemsPerSlot);
        
        public bool TryAddHotbarItem(MinecraftItemSO item, int count, int index)
        {
            if (!CanAddHotbarItem(item, count, index))
                return false;
            
            if (_hotbarItems[index] == null)
                _hotbarItems[index] = new RuntimeItem{ItemSO = item, Count = count};
            else
                _hotbarItems[index].Count += count;
            
            return true;
        }

        public bool TryAddInventoryItem(MinecraftItemSO item, int count, int index)
        {
            if (!CanAddInventoryItem(item, count, index))
                return false;
            
            if (_inventoryItems[index] == null)
                _inventoryItems[index] = new RuntimeItem{ItemSO = item, Count = count};
            else
                _inventoryItems[index].Count += count;
            return true;
        }

        public bool TryAddCraftTableItem(MinecraftItemSO item, int count, int index)
        {
            if (!CanAddCraftTableItem(item, count, index))
                return false;
            
            if (_craftTableItems[index] == null)
                _craftTableItems[index] = new RuntimeItem{ItemSO = item, Count = count};
            else
                _craftTableItems[index].Count += count;
            return true;
        }

        public bool CanRemoveHotbarItem(MinecraftItemSO item, int count, int index)
        {
            if (_hotbarItems[index] == null || _hotbarItems[index].ItemSO != item || _hotbarItems[index].Count < count)
                return false;
            return true;
        }

        public bool CanRemoveInventoryItem(MinecraftItemSO item, int count, int index)
        {
            if (_inventoryItems[index] == null || _inventoryItems[index].ItemSO != item || _inventoryItems[index].Count < count)
                return false;
            return true;
        }

        public bool CanRemoveCraftTableItem(MinecraftItemSO item, int count, int index)
        {
            if (_craftTableItems[index] == null || _craftTableItems[index].ItemSO != item || _craftTableItems[index].Count < count)
                return false;
            return true;
        }
        
        public bool TryRemoveHotbarItem(MinecraftItemSO item, int count, int index)
        {
            if (!CanRemoveHotbarItem(item, count, index))
                return false;
            
            _hotbarItems[index].Count -= count;
            if (_hotbarItems[index].Count <= 0)
                _hotbarItems[index] = null;
            return true;
        }

        public bool TryRemoveInventoryItem(MinecraftItemSO item, int count, int index)
        {
            if (!CanRemoveInventoryItem(item, count, index))
                return false;
            
            _inventoryItems[index].Count -= count;
            if (_inventoryItems[index].Count <= 0)                
                _inventoryItems[index] = null;
            return true;
        }

        public bool TryRemoveCraftTableItem(MinecraftItemSO item, int count, int index)
        {
            if (!CanRemoveCraftTableItem(item, count, index))
                return false;
            
            _craftTableItems[index].Count -= count;
            if (_craftTableItems[index].Count <= 0)                
                _craftTableItems[index] = null;
            return true;
        }
    }
}
