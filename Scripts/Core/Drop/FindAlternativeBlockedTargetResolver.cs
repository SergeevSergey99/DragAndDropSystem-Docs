using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Inventories;
using UDND.Slots;
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
            if (context == null || context.TargetItemAdapter == null)
                return BlockedTargetResolution.Reject();

            if (!_allowSameInventoryAlternativePlacement && context.IsSameInventorySlotDrop)
                return BlockedTargetResolution.Reject();

            // Preferred path: use pre-computed strategy-aware candidates (respects one-per-ID, rules).
            // The strategy already filtered eligibility; IAlternativePlacementStrategy only orders.
            if (context.AcceptanceCandidates != null)
            {
                var filteredSlots = BuildBaseSlotList(context.AcceptanceCandidates, context.ExcludedAlternativeBaseSlot);
                return BlockedTargetResolution.AlternativeSlots(
                    AlternativePlacementStrategy.EnumerateAlternativeSlots(
                        filteredSlots,
                        context.TargetItemAdapter,
                        context.ExcludedAlternativeBaseSlot,
                        (_, __) => true));
            }

            // Legacy path: filter all inventory slots via CanUseAlternativeSlot.
            if (context.TargetSlots == null || context.CanUseAlternativeSlot == null)
                return BlockedTargetResolution.Reject();

            return BlockedTargetResolution.AlternativeSlots(
                AlternativePlacementStrategy.EnumerateAlternativeSlots(
                    context.TargetSlots,
                    context.TargetItemAdapter,
                    context.ExcludedAlternativeBaseSlot,
                    context.CanUseAlternativeSlot));
        }

        private static List<BaseSlot> BuildBaseSlotList(IReadOnlyList<ISlot> candidates, BaseSlot excludeSlot)
        {
            var result = new List<BaseSlot>(candidates.Count);
            foreach (var slot in candidates)
            {
                if (excludeSlot != null && slot.Index == excludeSlot.Index &&
                    ReferenceEquals(slot.Inventory, excludeSlot.Inventory))
                    continue;
                var bs = slot as BaseSlot ?? slot.Inventory?.Slots?[slot.Index];
                if (bs != null)
                    result.Add(bs);
            }
            return result;
        }
    }
}
