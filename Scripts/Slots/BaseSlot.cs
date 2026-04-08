using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using UnityEngine;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Базовый класс для слотов. Наследуется от MonoBehaviour, чтобы можно было ссылаться в инспекторе.
    /// </summary>
    public abstract class BaseSlot : MonoBehaviour
    {
        [field: InfoBox("Правила фильтрации для этого конкретного слота. Оставьте пустым для слота без ограничений.")]
        [field: SerializeField, HideLabel, FoldoutGroup("Slot Rules", expanded: false)]
        public SlotRuleValidator SlotRuleValidator { get; protected set; } = new();
        
        public ItemStack Stack { get; protected set; } = ItemStack.Empty();
        public int Index { get; protected set; }
        public bool IsEmpty => Stack == null || Stack.IsEmpty;
        public virtual Transform Transform => transform;
        public IInventory Inventory { get; protected set; }

        /// <summary>
        /// Можно ли взаимодействовать со слотом (drag/drop).
        /// Используется FilterSortController для временного отключения слотов.
        /// </summary>
        public virtual bool IsInteractable { get; protected set; } = true;

        /// <summary>
        /// Событие изменения состояния интерактивности
        /// </summary>
        public event Action<bool> OnInteractableChanged;

        public virtual void Initialize(int index, IInventory inventory)
        {
            Inventory = inventory;
            Index = index;
            UpdateVisuals();
        }

        public virtual void SetStack(ItemStack stack)
        {
            Stack = stack ?? ItemStack.Empty();
            UpdateVisuals();
        }
        public virtual void Clear()
        {
            Stack = ItemStack.Empty();
            UpdateVisuals();
        }
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

        /// <summary>
        /// Временно скрыть/показать визуал слота (для анимаций автопереноса)
        /// </summary>
        public virtual void SetIconVisibility(bool b){}

        public virtual void Highlight(bool highlight) {}
        
        protected void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
            SlotRuleValidator?.OnValidate();
        }
    }
}
