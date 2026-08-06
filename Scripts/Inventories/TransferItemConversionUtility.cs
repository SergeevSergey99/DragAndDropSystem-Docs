using System.Collections.Generic;
using UDND.Core;

namespace UDND.Inventories
{
    /// <summary>
    /// Centralizes preview and execution-time adapter conversion for cross-inventory transfers.
    /// <para>
    /// Every conversion goes through the drag's <see cref="TransferConversionSession"/> when one is
    /// available, so a source adapter resolves to the same target-domain object for the whole drag.
    /// A null session is a supported mode, not a degraded one: code-driven transfers that never
    /// build a drag simply convert on the spot.
    /// </para>
    /// </summary>
    internal static class TransferItemConversionUtility
    {
        public static bool TryResolveTargetItem(
            IInventory sourceInventory,
            IInventory targetInventory,
            IItemAdapter sourceItemAdapter,
            out IItemAdapter targetItemAdapter)
            => TryResolveTargetItem(sourceInventory, targetInventory, sourceItemAdapter, null, out targetItemAdapter);

        public static bool TryResolveTargetItem(
            IInventory sourceInventory,
            IInventory targetInventory,
            IItemAdapter sourceItemAdapter,
            TransferConversionSession session,
            out IItemAdapter targetItemAdapter)
        {
            targetItemAdapter = sourceItemAdapter;
            if (sourceItemAdapter == null)
                return false;

            if (session != null)
            {
                return session.TryResolve(
                    sourceInventory,
                    targetInventory,
                    sourceItemAdapter,
                    adapter => ConvertAcrossBoundary(sourceInventory, targetInventory, adapter),
                    out targetItemAdapter);
            }

            targetItemAdapter = ConvertAcrossBoundary(sourceInventory, targetInventory, sourceItemAdapter);
            return targetItemAdapter != null;
        }

        /// <summary>
        /// Builds the target-domain view of <paramref name="previewCount"/> items of an entry.
        /// <para>
        /// The slice is taken from the tail, matching <c>ItemStack.Split</c> and
        /// <c>ItemStack.CreateCopy</c>: execution splits the last N adapters off the source stack,
        /// so a preview over the first N would resolve instances that never move and the promise
        /// of "the previewed object is the transferred object" would silently break for every
        /// partial transfer.
        /// </para>
        /// </summary>
        public static bool TryCreatePreviewStack(
            InventoryAcceptanceRequest request,
            int previewCount,
            IItemAdapter previewItemAdapter,
            out ItemStack previewStack)
        {
            previewStack = null;
            if (request == null || previewCount <= 0)
                return false;

            if (TryGetSourceSlice(request, previewCount, out var sourceSlice))
            {
                var convertedAdapters = new List<IItemAdapter>(sourceSlice.Count);
                foreach (var sourceAdapter in sourceSlice)
                {
                    if (!TryResolveTargetItem(
                            request.SourceInventory,
                            request.TargetInventory,
                            sourceAdapter,
                            request.ConversionSession,
                            out var convertedAdapter))
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
            // would corrupt stacks that carry per-instance runtime state. The conversion session
            // is deliberately bypassed here: it is keyed by source instance and would hand back
            // the very same object for all previewCount items.
            var syntheticAdapters = new List<IItemAdapter>(previewCount);
            for (int i = 0; i < previewCount; i++)
            {
                if (!TryResolveTargetItem(
                        request.SourceInventory,
                        request.TargetInventory,
                        previewAdapter,
                        out var convertedPreviewAdapter))
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

        /// <summary>
        /// The source-domain slice an entry would hand to execution, without any conversion.
        /// Used to build drop-rule validation input: the conversion to the target domain happens
        /// once, in the rule pipeline itself.
        /// <para>
        /// Returns false when the request carries no concrete source instances; such a request is
        /// already expressed in the target domain and has nothing to convert.
        /// </para>
        /// </summary>
        public static bool TryCreateSourceStack(
            InventoryAcceptanceRequest request,
            int count,
            out ItemStack sourceStack)
        {
            sourceStack = null;
            if (request == null || count <= 0)
                return false;

            return TryGetSourceSlice(request, count, out var sourceSlice) &&
                   ItemStack.TryCreate(sourceSlice, out sourceStack);
        }

        public static bool TryConvertOutgoingStack(IInventory sourceInventory, ItemStack stack)
            => TryConvertStack(stack, adapter => ConvertOutgoing(sourceInventory, adapter));

        public static bool TryConvertIncomingStack(IInventory targetInventory, ItemStack stack)
            => TryConvertStack(stack, adapter => ConvertIncoming(targetInventory, adapter));

        /// <summary>
        /// Converts a stack in place from the source domain to the target domain, reusing the
        /// objects this drag already resolved. This is the call that makes the committed instance
        /// identical to the previewed one.
        /// </summary>
        public static bool TryConvertStackToTargetDomain(
            IInventory sourceInventory,
            IInventory targetInventory,
            ItemStack stack,
            TransferConversionSession session)
        {
            return TryConvertStack(
                stack,
                adapter => TryResolveTargetItem(sourceInventory, targetInventory, adapter, session, out var converted)
                    ? converted
                    : null);
        }

        /// <summary>
        /// Releases the cached conversions for adapters that have just been committed into the
        /// target inventory: the converted objects belong to that inventory now and must not be
        /// offered to a later placement of the same drag.
        /// </summary>
        public static void ConsumeCommitted(
            IInventory sourceInventory,
            IInventory targetInventory,
            IReadOnlyList<IItemAdapter> committedSourceAdapters,
            TransferConversionSession session)
        {
            if (session == null || committedSourceAdapters == null)
                return;

            for (int i = 0; i < committedSourceAdapters.Count; i++)
                session.Consume(sourceInventory, targetInventory, committedSourceAdapters[i]);
        }

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

        /// <summary>
        /// The tail slice of the source entry stack, or false when the request carries no concrete
        /// source instances (generic acceptance queries, code-driven adds).
        /// </summary>
        private static bool TryGetSourceSlice(
            InventoryAcceptanceRequest request,
            int count,
            out List<IItemAdapter> slice)
        {
            slice = null;
            var sourceAdapters = request.SourceEntry?.Stack?.Adapters;
            if (sourceAdapters == null || sourceAdapters.Count < count)
                return false;

            slice = new List<IItemAdapter>(count);
            for (int i = sourceAdapters.Count - count; i < sourceAdapters.Count; i++)
                slice.Add(sourceAdapters[i]);

            return true;
        }

        private static IItemAdapter ConvertAcrossBoundary(
            IInventory sourceInventory,
            IInventory targetInventory,
            IItemAdapter sourceItemAdapter)
        {
            var intermediateItem = ConvertOutgoing(sourceInventory, sourceItemAdapter);
            return intermediateItem == null ? null : ConvertIncoming(targetInventory, intermediateItem);
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
