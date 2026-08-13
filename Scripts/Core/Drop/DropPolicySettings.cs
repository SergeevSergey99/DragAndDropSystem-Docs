using System;
using UnityEngine;
using UnityEngine.Serialization;
using UDND.Inventories;
using UDND.Tools.Inspector;

namespace UDND.Core
{
    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeField]
        private BlockedTargetResolutionKind _blockedTargetResolution =
            BlockedTargetResolutionKind.FindAlternative;

        [SerializeReference, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.FindAlternative),
         ManagedReferencePicker, InlineProperty, HideLabel]
        private PlacementCandidateOrderer _alternativeOrderer =
            new MergeFirstPlacementCandidateOrderer();

        [SerializeField, ShowIf(nameof(_blockedTargetResolution), BlockedTargetResolutionKind.FindAlternative),
         Tooltip("Allow a blocked same-inventory drop to use another placement.")]
        private bool _allowSameInventoryAlternativePlacement = false;

        [SerializeField, ShowIf(nameof(ShowShapedSwapSettings)),
         FormerlySerializedAs("_multiSwapMode"),
         Tooltip("How many placements one incoming item may displace. " +
                 "Only affects inventories with multi-cell footprints: where an item always " +
                 "occupies exactly one cell, both modes behave identically.")]
        private SwapDisplacementMode _swapDisplacement = SwapDisplacementMode.SinglePlacement;

        [SerializeField, ShowIf(nameof(ShowShapedSwapSettings)),
         Tooltip("What a swap does when the incoming footprint covers an item only partly. " +
                 "Reject allows clean exchanges only; WithDragOffset places the displaced item by " +
                 "its grab offset; VacatedArea searches the freed cells first, then their free " +
                 "neighbours, then the rest of the inventory.")]
        private PartialOverlapSwapMode _partialOverlapSwap = PartialOverlapSwapMode.Reject;

        /// <summary>
        /// The inventory these settings belong to. A serializable settings class cannot see whoever
        /// embeds it, so the owner hands itself over. Held rather than copied out into flags: a
        /// cached copy goes stale between the owner changing and the next push, and every further
        /// question the settings need to ask would cost another field.
        /// </summary>
        [NonSerialized] private UniversalInventory _owner;

        /// <summary>
        /// The two swap settings above only mean anything where an item can cover several cells, so
        /// they stay hidden elsewhere.
        /// <para>
        /// Deliberately one plain condition on a member of this very class: that is the only form
        /// every inspector framework resolves the same way, and <c>ShowIf</c> is bridged to Odin.
        /// </para>
        /// </summary>
        private bool ShowShapedSwapSettings =>
            _blockedTargetResolution == BlockedTargetResolutionKind.Swap &&
            _owner != null && _owner.UsesGridTopology;

        /// <summary>Called by the owning inventory so these settings can read its configuration.</summary>
        internal void SetOwner(UniversalInventory owner)
        {
            _owner = owner;
        }

        [SerializeField, Tooltip("Allow partial transfer if only part of one entry fits.")]
        private bool _allowPartial = true;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            return new ResolvedDropPolicy(
                requested?.BlockedTargetResolution ?? _blockedTargetResolution,
                requested?.AlternativeOrderer ?? _alternativeOrderer,
                requested?.AllowSameInventoryAlternativePlacement ?? _allowSameInventoryAlternativePlacement,
                requested?.PartialTransferMode ?? (_allowPartial
                    ? PartialTransferMode.Allow
                    : PartialTransferMode.RequireFull),
                requested?.SwapDisplacementMode ?? _swapDisplacement,
                requested?.PartialOverlapSwap ?? _partialOverlapSwap);
        }
    }
}
