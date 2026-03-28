using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Адаптер ItemInstance → IInventoryItem.
    /// Контейнеры не стакаются (MaxStackSize = 1).
    /// </summary>
    public class ContainerItemAdapter : IInventoryItem, IDescribable, IStackSizeLimitable
    {
        public readonly IContainerizeItemInstance Instance;

        public ContainerItemAdapter(IContainerizeItemInstance instance) => Instance = instance;

        // IInventoryItem
        public string ItemId => Instance.GetItem().GetInstanceID().ToString();
        public Sprite Icon => Instance.GetItem().Icon;
        public string DisplayName => Instance.GetItem().DisplayName;

        // IDescribable
        public string Description => Instance.GetItem().Description ?? "";

        // IStackSizeLimitable — контейнеры не стакаются
        public int MaxStackSize => Instance is ContainerItemInstance ? 1 : 64;
    }
}
