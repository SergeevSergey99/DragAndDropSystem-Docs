using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class FindAlternativeBlockedTargetResolver : BlockedTargetResolverBase
    {
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel]
        private IAlternativePlacementStrategy _alternativePlacementStrategy = new MergeFirstAlternativePlacementStrategy();
        [SerializeField, Tooltip("If disabled, dropping from a slot to another slot in the same inventory will not fall back to a different free slot.")]
        private bool _allowSameInventoryAlternativePlacement = true;

        public IAlternativePlacementStrategy AlternativePlacementStrategy =>
            _alternativePlacementStrategy ??= new MergeFirstAlternativePlacementStrategy();

        public bool AllowSameInventoryAlternativePlacement => _allowSameInventoryAlternativePlacement;

        public FindAlternativeBlockedTargetResolver()
        {
        }

        public FindAlternativeBlockedTargetResolver(
            IAlternativePlacementStrategy alternativePlacementStrategy,
            bool allowSameInventoryAlternativePlacement = true)
        {
            SetAlternativePlacementStrategy(alternativePlacementStrategy);
            _allowSameInventoryAlternativePlacement = allowSameInventoryAlternativePlacement;
        }

        public void SetAlternativePlacementStrategy(IAlternativePlacementStrategy strategy)
        {
            _alternativePlacementStrategy = strategy ?? new MergeFirstAlternativePlacementStrategy();
        }

        public override BlockedTargetResolution Resolve(BlockedTargetResolutionContext context)
        {
            if (context == null ||
                context.TargetSlots == null ||
                context.TargetItemAdapter == null ||
                context.CanUseAlternativeSlot == null)
                return BlockedTargetResolution.Reject();

            if (!_allowSameInventoryAlternativePlacement && context.IsSameInventorySlotDrop)
                return BlockedTargetResolution.Reject();

            return BlockedTargetResolution.AlternativeSlots(
                AlternativePlacementStrategy.EnumerateAlternativeSlots(
                    context.TargetSlots,
                    context.TargetItemAdapter,
                    context.ExcludedAlternativeBaseSlot,
                    context.CanUseAlternativeSlot));
        }
    }
}
