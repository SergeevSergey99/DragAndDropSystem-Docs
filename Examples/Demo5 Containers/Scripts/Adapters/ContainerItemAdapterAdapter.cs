using DragAndDropSystem.Core;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Adapter mapping ItemInstance to IItemAdapter.
    /// Containers are not stackable (MaxStackSize = 1).
    /// </summary>
    public class ContainerItemAdapterAdapter : IItemAdapter, IDescribable
    {
        public readonly IContainerizeItemInstance Instance;

        public ContainerItemAdapterAdapter(IContainerizeItemInstance instance) => Instance = instance;

        // IItemAdapter
        public string ItemId => Instance is ContainerItemInstance
            ? $"container:{Instance.GetHashCode()}"
            : $"itemAdapter:{Instance.GetItem().GetInstanceID()}";
        public Sprite Icon => Instance.GetItem().Icon;
        public string DisplayName => Instance.GetItem().DisplayName;

        // IDescribable
        public string Description => Instance.GetItem().Description ?? "";
    }
}
