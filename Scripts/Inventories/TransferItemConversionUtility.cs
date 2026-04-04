using System.Collections.Generic;
using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Centralizes preview and execution-time adapter conversion for cross-inventory transfers.
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

            var intermediateItem = ConvertOutgoing(sourceInventory, sourceItemAdapter);
            if (intermediateItem == null)
            {
                targetItemAdapter = null;
                return false;
            }

            targetItemAdapter = ConvertIncoming(targetInventory, intermediateItem);
            return targetItemAdapter != null;
        }

        public static bool TryCreatePreviewStack(
            InventoryAcceptanceRequest request,
            int previewCount,
            IItemAdapter previewItemAdapter,
            out ItemStack previewStack)
        {
            previewStack = null;
            if (request == null || previewCount <= 0)
                return false;

            var sourceAdapters = request.SourceEntry?.Stack?.Adapters;
            if (sourceAdapters != null && sourceAdapters.Count >= previewCount)
            {
                var convertedAdapters = new List<IItemAdapter>(previewCount);
                for (int i = 0; i < previewCount; i++)
                {
                    if (!TryResolveTargetItem(request.SourceInventory, request.TargetInventory, sourceAdapters[i], out var convertedAdapter))
                        return false;

                    convertedAdapters.Add(convertedAdapter);
                }

                return ItemStack.TryCreate(convertedAdapters, out previewStack);
            }

            var previewAdapter = previewItemAdapter ?? request.ItemAdapter;
            if (previewAdapter == null)
                return false;

            // Fallback for generic acceptance requests that have no concrete source instances.
            var syntheticAdapters = new List<IItemAdapter>(previewCount);
            for (int i = 0; i < previewCount; i++)
                syntheticAdapters.Add(previewAdapter);

            return ItemStack.TryCreate(syntheticAdapters, out previewStack);
        }

        public static bool TryConvertOutgoingStack(IInventory sourceInventory, ItemStack stack)
            => TryConvertStack(stack, adapter => ConvertOutgoing(sourceInventory, adapter));

        public static bool TryConvertIncomingStack(IInventory targetInventory, ItemStack stack)
            => TryConvertStack(stack, adapter => ConvertIncoming(targetInventory, adapter));

        private static bool TryConvertStack(ItemStack stack, System.Func<IItemAdapter, IItemAdapter> converter)
        {
            return stack != null && !stack.IsEmpty && stack.TryConvertAdapters(converter);
        }

        private static IItemAdapter ConvertOutgoing(IInventory inventory, IItemAdapter itemAdapter)
        {
            var converter = inventory?.DataBinding?.ItemConverter;
            return converter != null ? converter.TryConvertOutgoing(itemAdapter) : itemAdapter;
        }

        private static IItemAdapter ConvertIncoming(IInventory inventory, IItemAdapter itemAdapter)
        {
            var converter = inventory?.DataBinding?.ItemConverter;
            return converter != null ? converter.TryConvertIncoming(itemAdapter) : itemAdapter;
        }
    }
}
