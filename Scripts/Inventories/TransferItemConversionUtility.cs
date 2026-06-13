using System.Collections.Generic;
using UDND.Core;

namespace UDND.Inventories
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
            // We must still end up with distinct adapter objects; repeating the same reference
            // would corrupt stacks that carry per-instance runtime state.
            var syntheticAdapters = new List<IItemAdapter>(previewCount);
            for (int i = 0; i < previewCount; i++)
            {
                if (!TryResolveTargetItem(request.SourceInventory, request.TargetInventory, previewAdapter, out var convertedPreviewAdapter))
                    return false;

                if (convertedPreviewAdapter == null)
                    return false;

                // Only guard against duplicate references when a converter actually changed the
                // adapter (same-reference output is expected when there is no conversion and is
                // safe: preview stacks are ephemeral and discarded after CanPlace returns).
                if (!ReferenceEquals(convertedPreviewAdapter, previewAdapter) &&
                    ContainsReference(syntheticAdapters, convertedPreviewAdapter))
                    return false;

                syntheticAdapters.Add(convertedPreviewAdapter);
            }

            return ItemStack.TryCreate(syntheticAdapters, out previewStack);
        }

        public static bool TryConvertOutgoingStack(IInventory sourceInventory, ItemStack stack)
            => TryConvertStack(stack, adapter => ConvertOutgoing(sourceInventory, adapter));

        public static bool TryConvertIncomingStack(IInventory targetInventory, ItemStack stack)
            => TryConvertStack(stack, adapter => ConvertIncoming(targetInventory, adapter));

        public static bool TryCreateConvertedStack(
            IInventory sourceInventory,
            IInventory targetInventory,
            ItemStack sourceStack,
            out ItemStack convertedStack)
        {
            convertedStack = null;
            if (sourceStack == null || sourceStack.IsEmpty)
                return false;

            var convertedAdapters = new List<IItemAdapter>(sourceStack.Count);
            foreach (var adapter in sourceStack.Adapters)
            {
                if (!TryResolveTargetItem(sourceInventory, targetInventory, adapter, out var convertedAdapter))
                    return false;

                convertedAdapters.Add(convertedAdapter);
            }

            return ItemStack.TryCreate(convertedAdapters, out convertedStack);
        }

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

        private static bool ContainsReference(List<IItemAdapter> adapters, IItemAdapter candidate)
        {
            foreach (var adapter in adapters)
            {
                if (ReferenceEquals(adapter, candidate))
                    return true;
            }

            return false;
        }
    }
}
