using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Resolves target-side preview item for transfer planning/execution without mutating source stacks.
    /// </summary>
    internal static class TransferItemConversionUtility
    {
        public static bool TryResolveTargetItem(
            IInventory sourceInventory,
            IInventory targetInventory,
            IInventoryItem sourceItem,
            out IInventoryItem targetItem)
        {
            targetItem = sourceItem;
            if (sourceItem == null)
                return false;

            var intermediateItem = sourceItem;

            if (sourceInventory is UniversalInventory sourceUniversal &&
                !sourceUniversal.TryPreviewOutgoingItem(sourceItem, out intermediateItem))
            {
                targetItem = null;
                return false;
            }

            if (targetInventory is UniversalInventory targetUniversal &&
                !targetUniversal.TryPreviewIncomingItem(intermediateItem, out targetItem))
            {
                targetItem = null;
                return false;
            }

            targetItem ??= intermediateItem;
            return targetItem != null;
        }
    }
}
