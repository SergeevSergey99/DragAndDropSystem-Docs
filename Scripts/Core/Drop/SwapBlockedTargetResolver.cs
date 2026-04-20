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

        public override ISwapStrategy SwapStrategy => _swapStrategy ?? (_swapStrategy = new HintedTargetSwapStrategy());

        public void SetSwapStrategy(ISwapStrategy strategy)
        {
            _swapStrategy = strategy ?? new HintedTargetSwapStrategy();
        }
    }
}
