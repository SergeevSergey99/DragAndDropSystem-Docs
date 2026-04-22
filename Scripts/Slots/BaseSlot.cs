using System;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Slots
{
    /// <summary>
    /// Base class for slots. Inherits from MonoBehaviour so it can be referenced in the Inspector.
    /// </summary>
    public abstract class BaseSlot : MonoBehaviour
    {
        [field: InfoBox("Filtering rules for this specific slot. Leave empty for a slot without restrictions.")]
        [field: SerializeField, HideLabel, FoldoutGroup("Slot Rules", expanded: false)]
        public SlotRuleValidator SlotRuleValidator { get; protected set; } = new();
        
        public ItemStack Stack { get; protected set; } = ItemStack.Empty();
        public int Index { get; protected set; }
        public bool IsEmpty => Stack == null || Stack.IsEmpty;
        public virtual Transform Transform => transform;
        public IInventory Inventory { get; protected set; }

        /// <summary>
        /// Whether the slot can be interacted with (drag/drop).
        /// Used by FilterSortController to temporarily disable slots.
        /// </summary>
        public virtual bool IsInteractable { get; protected set; } = true;

        /// <summary>
        /// Event raised when the interactable state changes
        /// </summary>
        public event Action<bool> OnInteractableChanged;

        public virtual void Initialize(int index, IInventory inventory)
        {
            SetInventoryIndex(index, inventory);
        }

        public void SetInventoryIndex(int index, IInventory inventory)
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
        
        /// <summary>Highlight flag (hover / drop-preview). Preserved across UpdateVisuals.</summary>
        protected bool _isHighlighted;

        /// <summary>
        /// Icon visibility flag. Set by animation systems through
        /// <see cref="SetDragged"/> and applied on the next UpdateVisuals.
        /// </summary>
        protected bool _isDragged = false;
        
        /// <summary>
        /// Single entry point for a full visual refresh.
        /// Called after any slot data change.
        /// </summary>
        public virtual void UpdateVisuals()
        {
            if (IsEmpty)
                RenderEmpty();
            if (_isDragged)
                RenderFilledAndDragged();
            else
                RenderFilled();

            OnVisualsUpdated();
        }
        
        /// <summary>Render a non-empty slot: icon + counter.</summary>
        protected virtual void RenderFilled(){}

        /// <summary>Render a non-empty slot that is being dragged.</summary>
        protected virtual void RenderFilledAndDragged() => RenderEmpty();
        
        /// <summary>Render an empty slot: hide icon and counter.</summary>
        protected virtual void RenderEmpty(){}
        
        /// <summary>
        /// Hook for derived classes: called at the end of each UpdateVisuals.
        /// Use it to render additional elements (frames, effects, etc.)
        /// without needing to call base.UpdateVisuals().
        /// </summary>
        protected virtual void OnVisualsUpdated() { }
        
        /// <summary>
        /// Set the slot interactable state.
        /// Called by FilterSortController for filtering/sorting.
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
        /// Update visual state based on interactability.
        /// Override in derived classes for custom behavior.
        /// </summary>
        protected virtual void UpdateInteractableVisuals() {}

        /// <summary>
        /// Temporarily hide/show the icon (for auto-transfer animations).
        /// Preserves the flag and performs a full UpdateVisuals so other
        /// visual states are not reset.
        /// </summary>
        public virtual void SetDragged(bool dragged)
        {
            _isDragged = dragged;
            UpdateVisuals();
        }

        /// <summary>
        /// Enable/disable slot highlight (hover, drop-preview, etc.).
        /// The state is preserved and will not be reset by the next UpdateVisuals.
        /// </summary>
        public virtual void Highlight(bool highlight) {}
        
        protected void OnValidate()
        {
            // Sort rules when values change in the Inspector
            SlotRuleValidator?.OnValidate();
        }
    }
}
