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
        /// If true, this action may fire from a pointer event that occurred outside any slot
        /// (adapter == null). Default false: action only fires when a slot is hovered/focused.
        /// Implementations must tolerate null adapter and (possibly) null inventory.
        /// </summary>
        public virtual bool AllowOutOfSlot() => false;

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

        public override bool IsDragOnlyBinding() => true;
        
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

            var sourceSlot = adapter?.BaseSlot;
            if  (sourceSlot == null)
                return ActionResult.Failed("Source slot is not configured");
            
            DragAndDropManager.AutoCreateInstance.StartDrag(sourceSlot, _dragPolicyOverride.TryBuild());
            return ActionResult.Succeeded();
        }
    }
    [Serializable]
    public sealed class CompleteDragAction : AssetSafeSlotInteractionAction
    {
        [SerializeField] private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        public override bool IsDragOnlyBinding() => true;
        
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
            => DragAndDropManager.AutoCreateInstance.IsDragging;

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
                return ActionResult.Failed("Drag is not active");

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
