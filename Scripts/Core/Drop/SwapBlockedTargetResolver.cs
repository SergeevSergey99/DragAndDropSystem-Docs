using System;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class SwapBlockedTargetResolver : BlockedTargetResolverBase
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private ISwapStrategy _swapStrategy = new HintedTargetSwapStrategy();

        public ISwapStrategy SwapStrategy => _swapStrategy ?? (_swapStrategy = new HintedTargetSwapStrategy());

        public void SetSwapStrategy(ISwapStrategy strategy)
        {
            _swapStrategy = strategy ?? new HintedTargetSwapStrategy();
        }

        public override BlockedTargetResolution Resolve(BlockedTargetResolutionContext context)
        {
            if (context == null)
                return BlockedTargetResolution.Reject();

            var swapContext = new SwapSearchContext(
                context.DragContext,
                context.DragEntry,
                context.TargetInventory,
                context.HintedTargetBaseSlot,
                context.PreferHint);

            return BlockedTargetResolution.SwapTargets(SwapStrategy.EnumerateSwapTargets(swapContext));
        }
    }
}
