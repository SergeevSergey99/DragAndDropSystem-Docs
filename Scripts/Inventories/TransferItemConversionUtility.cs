using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Resolves target-side preview itemAdapter for transfer planning/execution without mutating source stacks.
    /// </summary>
    internal static class TransferItemConversionUtility
    {
        public static bool TryResolveTargetItem(
            IInventory sourceInventory,
            IInventory targetInventory,
            IItemAdapter sourceItemAdapter,
            out IItemAdapter targetItemAdapter)
        {
            targetItemAdapter = sourceItemAdapter;
            if (sourceItemAdapter == null)
                return false;

            var intermediateItem = sourceItemAdapter;

            if (sourceInventory is UniversalInventory sourceUniversal &&
                !sourceUniversal.TryPreviewOutgoingItem(sourceItemAdapter, out intermediateItem))
            {
                targetItemAdapter = null;
                return false;
            }

            if (targetInventory is UniversalInventory targetUniversal &&
                !targetUniversal.TryPreviewIncomingItem(intermediateItem, out targetItemAdapter))
            {
                targetItemAdapter = null;
                return false;
            }

            targetItemAdapter ??= intermediateItem;
            return targetItemAdapter != null;
        }
    }
}
