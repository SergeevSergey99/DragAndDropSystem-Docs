using System;
using System.Collections.Generic;
using CodeUtils;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Examples.Craft
{
    public class CraftingManager : MonoSingleton<CraftingManager>
    {
        [SerializeReference, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _hotbarItems = new RuntimeItem[9];
        [SerializeReference, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _inventoryItems = new RuntimeItem[27];
        [SerializeReference, FixedArraySize, ManagedReferencePicker] private RuntimeItem[] _craftTableItems = new RuntimeItem[9];

        [SerializeField] private List<CraftingRecipeSO> _recipes;

        public const int MaxItemsPerSlot = 64;

        private CraftingRecipeSO _currentRecipe;
        private int _craftMultiplier;

        public IReadOnlyList<RuntimeItem> HotbarItems => _hotbarItems;
        public IReadOnlyList<RuntimeItem> InventoryItems => _inventoryItems;
        public IReadOnlyList<RuntimeItem> CraftTableItems => _craftTableItems;

        public CraftingRecipeSO CurrentRecipe => _currentRecipe;

        /// <summary>How many times the current recipe can be crafted with the available ingredients.</summary>
        public int CraftMultiplier => _craftMultiplier;

        /// <summary>Craft result changed (appeared/disappeared/recipe changed).</summary>
        public event Action OnCraftResultChanged;

        /// <summary>Crafting table data changed (ingredients were consumed).</summary>
        public event Action OnCraftTableChanged;
        
        public bool CanAddHotbarItem(CraftItemSO item, int count, int index) =>
            _hotbarItems[index] == null || (_hotbarItems[index].ItemSO == item && _hotbarItems[index].Count + count <= MaxItemsPerSlot);
        public bool CanAddInventoryItem(CraftItemSO item, int count, int index) =>
            _inventoryItems[index] == null || (_inventoryItems[index].ItemSO == item && _inventoryItems[index].Count + count <= MaxItemsPerSlot);
        public bool CanAddCraftTableItem(CraftItemSO item, int count, int index) =>
            _craftTableItems[index] == null || (_craftTableItems[index].ItemSO == item && _craftTableItems[index].Count + count <= MaxItemsPerSlot);
        
        public bool TryAddHotbarItem(CraftItemSO item, int count, int index)
        {
            if (!CanAddHotbarItem(item, count, index))
                return false;
            
            if (_hotbarItems[index] == null)
                _hotbarItems[index] = new RuntimeItem{ItemSO = item, Count = count};
            else
                _hotbarItems[index].Count += count;
            
            return true;
        }

        public bool TryAddInventoryItem(CraftItemSO item, int count, int index)
        {
            if (!CanAddInventoryItem(item, count, index))
                return false;
            
            if (_inventoryItems[index] == null)
                _inventoryItems[index] = new RuntimeItem{ItemSO = item, Count = count};
            else
                _inventoryItems[index].Count += count;
            return true;
        }

        public bool TryAddCraftTableItem(CraftItemSO item, int count, int index)
        {
            if (!CanAddCraftTableItem(item, count, index))
                return false;
            
            if (_craftTableItems[index] == null)
                _craftTableItems[index] = new RuntimeItem{ItemSO = item, Count = count};
            else
                _craftTableItems[index].Count += count;
            RefreshCraftResult();
            return true;
        }

        public bool CanRemoveHotbarItem(CraftItemSO item, int count, int index)
        {
            if (_hotbarItems[index] == null || _hotbarItems[index].ItemSO != item || _hotbarItems[index].Count < count)
                return false;
            return true;
        }

        public bool CanRemoveInventoryItem(CraftItemSO item, int count, int index)
        {
            if (_inventoryItems[index] == null || _inventoryItems[index].ItemSO != item || _inventoryItems[index].Count < count)
                return false;
            return true;
        }

        public bool CanRemoveCraftTableItem(CraftItemSO item, int count, int index)
        {
            if (_craftTableItems[index] == null || _craftTableItems[index].ItemSO != item || _craftTableItems[index].Count < count)
                return false;
            return true;
        }
        
        public bool TryRemoveHotbarItem(CraftItemSO item, int count, int index)
        {
            if (!CanRemoveHotbarItem(item, count, index))
                return false;
            
            _hotbarItems[index].Count -= count;
            if (_hotbarItems[index].Count <= 0)
                _hotbarItems[index] = null;
            return true;
        }

        public bool TryRemoveInventoryItem(CraftItemSO item, int count, int index)
        {
            if (!CanRemoveInventoryItem(item, count, index))
                return false;
            
            _inventoryItems[index].Count -= count;
            if (_inventoryItems[index].Count <= 0)                
                _inventoryItems[index] = null;
            return true;
        }

        public bool TryRemoveCraftTableItem(CraftItemSO item, int count, int index)
        {
            if (!CanRemoveCraftTableItem(item, count, index))
                return false;

            _craftTableItems[index].Count -= count;
            if (_craftTableItems[index].Count <= 0)
                _craftTableItems[index] = null;
            RefreshCraftResult();
            return true;
        }

        /// <summary>
        /// Check recipes and update the current crafting result.
        /// Call after every crafting table change.
        /// </summary>
        public void RefreshCraftResult()
        {
            var grid = new CraftItemSO[9];
            var counts = new int[9];
            for (int i = 0; i < 9; i++)
            {
                grid[i] = _craftTableItems[i]?.ItemSO;
                counts[i] = _craftTableItems[i]?.Count ?? 0;
            }

            var prev = _currentRecipe;
            var prevMultiplier = _craftMultiplier;
            _currentRecipe = null;
            _craftMultiplier = 0;

            if (_recipes != null)
            {
                foreach (var recipe in _recipes)
                {
                    if (recipe != null && recipe.Matches(grid))
                    {
                        _currentRecipe = recipe;
                        _craftMultiplier = recipe.ComputeMaxCrafts(grid, counts);
                        break;
                    }
                }
            }

            if (_currentRecipe != prev || _craftMultiplier != prevMultiplier)
                OnCraftResultChanged?.Invoke();
        }

        /// <summary>
        /// Consume ingredients for the specified number of crafts according to CurrentRecipe.
        /// craftsToConsume is how many times to execute the recipe.
        /// </summary>
        public void ConsumeCraftIngredients(int craftsToConsume)
        {
            if (craftsToConsume <= 0 || _currentRecipe == null)
                return;

            var grid = new CraftItemSO[9];
            for (int i = 0; i < 9; i++)
                grid[i] = _craftTableItems[i]?.ItemSO;

            var consumePerCraft = _currentRecipe.GetConsumeAmountsPerCraft(grid);

            for (int i = 0; i < _craftTableItems.Length; i++)
            {
                int toConsume = consumePerCraft[i] * craftsToConsume;
                if (toConsume <= 0 || _craftTableItems[i] == null)
                    continue;

                _craftTableItems[i].Count -= toConsume;
                if (_craftTableItems[i].Count <= 0)
                    _craftTableItems[i] = null;
            }

            OnCraftTableChanged?.Invoke();
            RefreshCraftResult();
        }
    }
}
