using System;
using UDND.Core;
using UDND.Interaction;
using UDND.Tools.Inspector;

namespace UDND.Selection
{
    [Serializable]
    public sealed class SelectionSlotAction : AssetSafeSlotInteractionAction
    {
        [UnityEngine.SerializeReference, ManagedReferencePicker] private SelectionOperationBase _operation;
        
        public override bool AllowOutOfSlot() => _operation.AllowOutOfSlot();

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            if (_operation == null || !SelectionManager.IsInstanceExist)
                return ActionResult.Failed("Selection operation is not available");

            var manager = SelectionManager.AutoCreateInstance;
            var slot = snapshot?.ActiveSlot;
            if (_operation.CanExecute(manager, slot))
            {
                _operation.Execute(manager, slot);
                return ActionResult.Succeeded();
            }

            return ActionResult.Failed("Selection operation cannot execute");
        }
    }
}
