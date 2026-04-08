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
        
        /// <summary>Флаг подсветки (hover / drop-preview). Сохраняется между UpdateVisuals.</summary>
        protected bool _isHighlighted;

        /// <summary>
        /// Флаг видимости иконки. Выставляется анимационными системами через
        /// <see cref="SetIconVisibility"/> и учитывается при следующем UpdateVisuals.
        /// </summary>
        protected bool _iconVisible = true;
        
        /// <summary>
        /// Единая точка входа для полного обновления визуала.
        /// Вызывается после любого изменения данных слота.
        /// </summary>
        public virtual void UpdateVisuals()
        {
            if (_iconVisible && !IsEmpty)
                RenderFilled();
            else
                RenderEmpty();

            OnVisualsUpdated();
        }
        
        /// <summary>Рендер непустого слота: иконка + счётчик.</summary>
        protected virtual void RenderFilled(){}
        
        /// <summary>Рендер пустого слота: убираем иконку и счётчик.</summary>
        protected virtual void RenderEmpty(){}
        
        /// <summary>
        /// Хук для дочерних классов: вызывается в конце каждого UpdateVisuals.
        /// Используйте для рендера дополнительных элементов (рамки, эффекты и т.п.)
        /// без необходимости вызывать base.UpdateVisuals().
        /// </summary>
        protected virtual void OnVisualsUpdated() { }
        
        /// <summary>
        /// Установить состояние интерактивности слота.
        /// Вызывается FilterSortController для фильтрации/сортировки.
        /// </summary>
        public virtual void SetInteractable(bool interactable)
        {
            if (IsInteractable == interactable)
                return;

            IsInteractable = interactable;
            UpdateInteractableVisuals();
            OnInteractableChanged?.Invoke(interactable);
        }

        /// <summary>
        /// Обновить визуальное состояние в зависимости от интерактивности.
        /// Переопределите в наследниках для кастомного поведения.
        /// </summary>
        protected virtual void UpdateInteractableVisuals() {}

        /// <summary>
        /// Временно скрыть/показать иконку (для анимаций авто-переноса).
        /// Сохраняет флаг и делает полный UpdateVisuals, чтобы остальные
        /// визуальные состояния не сбросились.
        /// </summary>
        public virtual void SetIconVisibility(bool visible)
        {
            _iconVisible = visible;
            UpdateVisuals();
        }

        /// <summary>
        /// Включить/выключить подсветку слота (hover, drop-preview и т.п.).
        /// Состояние сохраняется — следующий UpdateVisuals его не сбросит.
        /// </summary>
        public virtual void Highlight(bool highlight) {}
        
        protected void OnValidate()
        {
            // Сортируем правила при изменении в Inspector
            SlotRuleValidator?.OnValidate();
        }
    }
}
