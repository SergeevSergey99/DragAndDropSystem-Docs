using System;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;
using UDND.Tools.Inspector;

namespace UDND.Slots
{
    /// <summary>
    /// Base class for slots. Inherits from MonoBehaviour so it can be referenced in the Inspector.
    /// </summary>
    public abstract class BaseSlot : MonoBehaviour
    {
        [field: InfoBox("Filtering rules for this specific slot. Leave empty for a slot without restrictions.")]
        [field: SerializeField, HideLabel, FoldoutGroup("Slot Rules", expanded: false)]
        public SlotRuleValidator SlotRuleValidator { get; protected set; } = new();
        
        /// <summary>
        /// Read-only view of the stack currently shown in this slot.
        /// Use inventory/store APIs for mutations so placement bookkeeping stays consistent.
        /// </summary>
        public virtual IReadOnlyItemStack Stack
        {
            get
            {
                if (Inventory != null &&
                    Inventory.TryGetStackForSlot(this, out var placementStack))
                    return placementStack ?? ItemStack.Empty();

                return ItemStack.Empty();
            }
        }

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
            if (Inventory != null)
            {
                if (!Inventory.TrySetStackForSlot(this, stack ?? ItemStack.Empty()))
                    Debug.LogWarning($"[{name}] SetStack failed for placement-backed slot {Index}");

                UpdateVisuals();
                return;
            }

            Debug.LogWarning($"[{name}] SetStack ignored: BaseSlot requires an inventory-backed stack store.");
            UpdateVisuals();
        }

        public virtual void Clear()
        {
            if (Inventory != null)
            {
                if (!Inventory.TryClearSlot(this))
                    Debug.LogWarning($"[{name}] Clear failed for placement-backed slot {Index}");

                UpdateVisuals();
                return;
            }

            Debug.LogWarning($"[{name}] Clear ignored: BaseSlot requires an inventory-backed stack store.");
            UpdateVisuals();
        }
        
        /// <summary>Highlight flag (hover / drop-preview). Preserved across UpdateVisuals.</summary>
        protected bool _isHighlighted;

        /// <summary>
        /// Source-side drag flag. Applied on the next UpdateVisuals.
        /// </summary>
        protected bool _isDraggedFrom = false;

        /// <summary>
        /// Target-side transfer flag. Applied on the next UpdateVisuals.
        /// </summary>
        protected bool _isDraggedTo = false;

        public bool IsDraggedFromVisualState => _isDraggedFrom;
        public bool IsDraggedToVisualState => _isDraggedTo;
        
        /// <summary>
        /// Single entry point for a full visual refresh.
        /// Called after any slot data change.
        /// </summary>
        public virtual void UpdateVisuals()
        {
            if (IsEmpty) RenderEmpty();
            else if (_isDraggedFrom) RenderFilledAndDraggedFrom();
            else if (_isDraggedTo) RenderFilledAndDraggedTo();
            else RenderFilled();

            OnVisualsUpdated();
        }
        
        /// <summary>Render a non-empty slot: icon + counter.</summary>
        protected virtual void RenderFilled(){}

        /// <summary>Render a non-empty slot that is the source of a drag/transfer.</summary>
        protected virtual void RenderFilledAndDraggedFrom()
        {
            if (Stack.Count > 0) RenderFilled();
            else RenderEmpty();
        }
        /// <summary>Render a non-empty slot that is the target of a transfer animation.</summary>
        protected virtual void RenderFilledAndDraggedTo()
        {
            if (Stack.Count > 1) RenderFilled();
            else RenderEmpty();
        }

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
        /// Marks this slot as the source of a drag/transfer.
        /// Default source rendering hides the content to avoid duplicate visuals.
        /// </summary>
        public virtual void SetDraggedFrom(bool dragged)
        {
            _isDraggedFrom = dragged;
            UpdateVisuals();
        }

        /// <summary>
        /// Marks this slot as the target of a transfer animation.
        /// Default target rendering keeps the slot visible; override
        /// RenderFilledAndDraggedTo for custom previews.
        /// </summary>
        public virtual void SetDraggedTo(bool dragged)
        {
            _isDraggedTo = dragged;
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
