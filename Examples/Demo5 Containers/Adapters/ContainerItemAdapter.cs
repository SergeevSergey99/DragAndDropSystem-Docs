using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Адаптер ItemInstance → IInventoryItem.
    /// Контейнеры не стакаются (MaxStackSize = 1).
    /// </summary>
    public class ContainerItemAdapter : IInventoryItem, IDescribable, IFilterable, IStackSizeLimitable
    {
        public readonly ItemInstance Instance;

        public ContainerItemAdapter(ItemInstance instance) => Instance = instance;

        // IInventoryItem
        public string ItemId => Instance.ItemSO.GetInstanceID().ToString();
        public Sprite Icon => Instance.ItemSO.Icon;
        public string DisplayName => Instance.ItemSO.DisplayName;

        // IDescribable
        public string Description
        {
            get
            {
                var desc = Instance.ItemSO.Description ?? "";
                if (Instance.IsContainer)
                    desc += $"\nContainer ({Instance.Contents.Count}/{Instance.Capacity})";
                return desc;
            }
        }

        // IFilterable
        public string Category => Instance.ItemSO.Category ?? "";
        public string Subcategory => "";
        public int Rarity => 0;

        // IStackSizeLimitable — контейнеры не стакаются
        public int MaxStackSize => Instance.IsContainer ? 1 : 64;
    }
}
