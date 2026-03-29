using DragAndDropSystem.Core;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Адаптер ItemInstance → IInventoryItem.
    /// Контейнеры не стакаются (MaxStackSize = 1).
    /// </summary>
    public class ContainerItemAdapter : IInventoryItem, IDescribable
    {
        public readonly IContainerizeItemInstance Instance;

        public ContainerItemAdapter(IContainerizeItemInstance instance) => Instance = instance;

        // IInventoryItem
        public string ItemId => Instance is ContainerItemInstance
            ? $"container:{Instance.GetHashCode()}"
            : $"item:{Instance.GetItem().GetInstanceID()}";
        public Sprite Icon => Instance.GetItem().Icon;
        public string DisplayName => Instance.GetItem().DisplayName;

        // IDescribable
        public string Description => Instance.GetItem().Description ?? "";
    }
}
