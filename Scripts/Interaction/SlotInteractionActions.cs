using System;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Interaction
{
    [Serializable]
    public abstract class SlotInteractionAction
    {
        public virtual string DisplayName => GetType().Name.Replace("Action", string.Empty);
        
        public virtual bool IsDragOnlyBinding() => false;

        /// <summary>
        /// If true, this action may fire when there is no concrete target in the interaction snapshot.
        /// This includes pointer events outside a slot, keyboard/input-action execution while a DropArea is focused,
        /// and inventory-level drag actions that operate through the active drop target.
        /// Implementations must tolerate missing slot and (possibly) null inventory.
        /// </summary>
        public virtual bool AllowOutOfSlot() => false;

        public virtual bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => snapshot?.Inventory != null;

        public virtual ActionResult Execute(RuntimeInteractionSnapshot snapshot)
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
            if (snapshot?.Inventory == null || snapshot.IsDragging)
                return false;

            var slot = snapshot.ActiveSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is already active");

            var sourceSlot = snapshot?.ActiveSlot;
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
        
        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
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

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            return DragAndDropManager.AutoCreateInstance.SplitDrop(_dropPolicyOverride.TryBuild(), _splitCount)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Split drop failed");
        }
    }

    [Serializable]
    public sealed class RotateDragAction : AssetSafeSlotInteractionAction
    {
        [SerializeField, Tooltip("How many 90-degree clockwise steps to apply to the active drag.")]
        private RotationStep _step = RotationStep.Clockwise90;

        public override bool IsDragOnlyBinding() => true;
        public override bool AllowOutOfSlot() => true;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => snapshot != null && snapshot.IsDragging;

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            DragAndDropManager.AutoCreateInstance.ActivateDropTargetForSlot(snapshot?.HoveredSlot ?? snapshot?.FocusedSlot ?? snapshot?.ActiveSlot);
            return DragAndDropManager.AutoCreateInstance.RotateCurrentDrag((int)_step)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Rotate drag failed");
        }

        private enum RotationStep
        {
            Clockwise90 = 1,
            Clockwise180 = 2,
            Clockwise270 = 3
        }
    }

    [Serializable]
    public sealed class CancelDragAction : AssetSafeSlotInteractionAction
    {
        public override bool IsDragOnlyBinding() => true;
        public override bool AllowOutOfSlot() => true;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
            => snapshot != null && snapshot.IsDragging;

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
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

            var slot = snapshot.ActiveSlot ?? ResolveAutoTransferSlot(snapshot.Inventory);
            return _sceneAction.CanExecute(snapshot.Inventory, slot);
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (_sceneAction == null || snapshot?.Inventory == null)
                return ActionResult.Failed("Inventory action is not configured");

            var slot = snapshot.ActiveSlot ?? ResolveAutoTransferSlot(snapshot.Inventory);
            if (!_sceneAction.CanExecute(snapshot.Inventory, slot))
                return ActionResult.Failed("Inventory action cannot execute");

            return _sceneAction.Execute(snapshot.Inventory, slot);
        }

        private static BaseSlot ResolveAutoTransferSlot(IInventory inventory)
        {
            return inventory is IInventoryInteractionSurface interactionSurface
                ? interactionSurface.ResolveAutoTransferSlot()
                : null;
        }
    }
}
