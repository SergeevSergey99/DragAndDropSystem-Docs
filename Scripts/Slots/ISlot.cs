using System;
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

        /// <summary>
        /// Можно ли взаимодействовать со слотом (drag/drop).
        /// Используется FilterSortController для временного отключения слотов.
        /// </summary>
        public virtual bool IsInteractable { get; protected set; } = true;

        /// <summary>
        /// Событие изменения состояния интерактивности
        /// </summary>
        public event Action<bool> OnInteractableChanged;

        public abstract void Initialize(int index, IInventory inventory);
        public abstract void SetStack(ItemStack stack);
        public abstract void ReplaceItem(IItemAdapter newItemAdapter);
        public abstract void Clear();
        public abstract void UpdateVisuals();
        
        /// <summary>
        /// Установить состояние интерактивности слота.
        /// Вызывается FilterSortController для фильтрации/сортировки.
        /// </summary>
        public virtual void SetInteractable(bool interactable)
        {
            if (IsInteractable == interactable)
                return;

            IsInteractable = interactable;
            OnInteractableChanged?.Invoke(interactable);
            UpdateInteractableVisuals();
        }

        /// <summary>
        /// Обновить визуальное состояние в зависимости от интерактивности.
        /// Переопределите в наследниках для кастомного поведения.
        /// </summary>
        protected virtual void UpdateInteractableVisuals()
        {
            // По умолчанию ничего не делаем - наследники могут переопределить
        }

        public int GetDragAmount() => Inventory?.GetDragAmount(this) ?? 0;
    }
}
