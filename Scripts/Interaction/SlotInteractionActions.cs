using System;
using UniversalDragAndDrop.Slots;
using UnityEngine;
using UnityEngine.EventSystems;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Interaction
{
    [Serializable]
    public abstract class SlotInteractionAction
    {
        public virtual string DisplayName => GetType().Name.Replace("Action", string.Empty);
        
        public virtual bool IsDragOnlyBinding() => false;

        /// <summary>
        /// If true, this action may fire when there is no concrete slot in the interaction snapshot.
        /// This includes pointer events outside a slot, keyboard/input-action execution while a DropArea is focused,
        /// and inventory-level drag actions that operate through the active drop target.
        /// Implementations must tolerate null adapter and (possibly) null inventory.
        /// </summary>
        public virtual bool AllowOutOfSlot() => false;

        public virtual bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => CanExecute(snapshot?.Inventory, ResolveAdapter(snapshot?.ResolvedBaseSlot), snapshot?.PointerEventData);

        public virtual ActionResult Execute(RuntimeInteractionSnapshot snapshot)
            => Execute(snapshot?.Inventory, ResolveAdapter(snapshot?.ResolvedBaseSlot), snapshot?.PointerEventData);

        private static SlotInputAdapter ResolveAdapter(BaseSlot baseSlot)
            => baseSlot != null ? baseSlot.GetComponent<SlotInputAdapter>() : null;

        public virtual bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => inventory != null;

        public virtual ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => ActionResult.Failed("Action is not implemented");
    }

    [Serializable]
    public abstract class AssetSafeSlotInteractionAction : SlotInteractionAction {}

    [Serializable]
    public sealed class StartDragAction : AssetSafeSlotInteractionAction
    {
        [SerializeField, Tooltip("Temporary item amount override for the current StartDrag. Applied to each selected source slot.")]
        private DragRequestPolicySettings _dragPolicyOverride = new DragRequestPolicySettings();

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsDragging)
                return false;

            var slot = snapshot.ResolvedBaseSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = adapter?.BaseSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is already active");

            var sourceSlot = adapter?.BaseSlot;
            if (sourceSlot == null || sourceSlot.IsEmpty || !sourceSlot.IsInteractable)
                return ActionResult.Failed("Source slot is empty or not interactable");

            return DragAndDropManager.AutoCreateInstance.StartDrag(sourceSlot, _dragPolicyOverride.TryBuild())
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Start drag failed");
        }
    }
    [Serializable]
    public sealed class CompleteDragAction : AssetSafeSlotInteractionAction
    {
        [SerializeField] private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        public override bool IsDragOnlyBinding() => true;
        public override bool AllowOutOfSlot() => true;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => snapshot != null && snapshot.IsDragging;
        
        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            DragAndDropManager.AutoCreateInstance.CompleteDrag(_dropPolicyOverride.TryBuild());
            return ActionResult.Succeeded();
        }
    }

    [Serializable]
    public sealed class SplitDropAction : AssetSafeSlotInteractionAction
    {
        [SerializeField, Min(1), Tooltip("Number of items to split off and drop.")]
        private int _splitCount = 1;
        [SerializeField] private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        public override bool IsDragOnlyBinding() => true;
        public override bool AllowOutOfSlot() => true;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => snapshot != null && snapshot.IsDragging;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            return DragAndDropManager.AutoCreateInstance.SplitDrop(_dropPolicyOverride.TryBuild(), _splitCount)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Split drop failed");
        }
    }

    [Serializable]
    public sealed class CancelDragAction : AssetSafeSlotInteractionAction
    {
        public override bool IsDragOnlyBinding() => true;
        public override bool AllowOutOfSlot() => true;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => snapshot != null && snapshot.IsDragging;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            DragAndDropManager.AutoCreateInstance.CancelDrag();
            return ActionResult.Succeeded();
        }
    }

    [Serializable]
    public sealed class InventorySlotAction : SlotInteractionAction
    {
        [SerializeField, Tooltip("Scene action component (MonoBehaviour).")]
        private InventoryActionBase _sceneAction;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
        {
            if (_sceneAction == null || snapshot?.Inventory == null)
                return false;

            var slot = snapshot.ResolvedBaseSlot ?? snapshot.Inventory.ResolveAutoTransferSlot();
            return _sceneAction.CanExecute(snapshot.Inventory, slot);
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (_sceneAction == null || snapshot?.Inventory == null)
                return ActionResult.Failed("Inventory action is not configured");

            var slot = snapshot.ResolvedBaseSlot ?? snapshot.Inventory.ResolveAutoTransferSlot();
            if (!_sceneAction.CanExecute(snapshot.Inventory, slot))
                return ActionResult.Failed("Inventory action cannot execute");

            return _sceneAction.Execute(snapshot.Inventory, slot);
        }

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_sceneAction == null || inventory == null)
                return false;

            var slot = adapter?.BaseSlot ?? inventory.ResolveAutoTransferSlot();
            return _sceneAction.CanExecute(inventory, slot);
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (_sceneAction == null || inventory == null)
                return ActionResult.Failed("Inventory action is not configured");

            var slot = adapter?.BaseSlot ?? inventory.ResolveAutoTransferSlot();
            if (!_sceneAction.CanExecute(inventory, slot))
                return ActionResult.Failed("Inventory action cannot execute");

            return _sceneAction.Execute(inventory, slot);
        }
    }
}
