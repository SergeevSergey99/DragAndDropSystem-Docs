using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using UnityEngine;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Базовый класс для слотов. Наследуется от MonoBehaviour, чтобы можно было ссылаться в инспекторе.
    /// </summary>
    public abstract class ISlot : MonoBehaviour
    {
        public abstract ItemStack Stack { get; }
        public abstract int Index { get; }
        public abstract bool IsEmpty { get; }
        public virtual Transform Transform => transform;
        public abstract IInventory Inventory { get; }
        public abstract SlotRuleValidator SlotRuleValidator { get; }

        public abstract void Initialize(int index, IInventory inventory);
        public abstract void SetStack(ItemStack stack);
        public abstract void ReplaceItem(IInventoryItem newItem);
        public abstract void Clear();
        public abstract void UpdateVisuals();
    }
}
