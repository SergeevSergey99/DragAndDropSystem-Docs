using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Rules;

namespace UDND.Examples.ShapedItems
{
    [Serializable]
    public struct ShapedPlacementSeed
    {
        public ShapedItemExampleSO item;
        [Min(0)] public int anchorIndex;
        public int orientation;

        public ShapedPlacementSeed(ShapedItemExampleSO item, int anchorIndex, int orientation = 0)
        {
            this.item = item;
            this.anchorIndex = Mathf.Max(0, anchorIndex);
            this.orientation = orientation;
        }
    }

    public class ShapedItemsInventoryDataBinding : PlacementInventoryDataBinding<ShapedItemExampleSO, ShapedItemAdapter>
    {
        [Header("Data")]
        [SerializeField] private List<ShapedPlacementSeed> _placements = new();

        protected override IEnumerable<PlacementData<ShapedItemExampleSO>> GetPlacements()
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                var seed = _placements[i];
                if (seed.item != null)
                    yield return new PlacementData<ShapedItemExampleSO>(
                        seed.item,
                        seed.anchorIndex,
                        1,
                        seed.orientation);
            }
        }

        protected override ShapedItemAdapter CreateAdapter(ShapedItemExampleSO item) => item != null ? new ShapedItemAdapter(item) : null;
        protected override ShapedItemExampleSO ExtractData(ShapedItemAdapter adapter) => adapter?.item;

        protected override void AddPlacementData(PlacementCommitContext<ShapedItemExampleSO, ShapedItemAdapter> context)
        {
            if (context.Data == null)
                return;

            _placements.Add(new ShapedPlacementSeed(
                context.Data,
                Mathf.Max(0, context.AnchorIndex),
                context.Orientation));
        }

        protected override void RemovePlacementData(PlacementCommitContext<ShapedItemExampleSO, ShapedItemAdapter> context)
        {
            if (context.Data == null)
                return;

            RemoveFirstPlacement(context.Data, context.AnchorIndex, context.Orientation);
        }

        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            if (entry.Stack?.PrimaryAdapter is not ShapedItemAdapter)
                return RuleResult.Failure("Only Demo6 shaped items are supported by this binding.");

            return base.CanStartDrag(context, entry);
        }

        public void ReplacePlacements(IEnumerable<ShapedPlacementSeed> placements, bool reload = true)
        {
            _placements.Clear();
            if (placements != null)
                _placements.AddRange(placements);

            if (reload)
                ReloadUI();
        }

        private void RemoveFirstPlacement(ShapedItemExampleSO item, int anchorIndex, int orientation)
        {
            if (TryRemoveFirstPlacement(item, anchorIndex, orientation, requireOrientation: true)) return;
            if (TryRemoveFirstPlacement(item, anchorIndex, orientation, requireOrientation: false)) return;

            TryRemoveFirstPlacement(item, -1, orientation, requireOrientation: false);
        }

        private bool TryRemoveFirstPlacement(ShapedItemExampleSO item, int anchorIndex, int orientation, bool requireOrientation)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (!ReferenceEquals(_placements[i].item, item))
                    continue;

                if (anchorIndex >= 0 && _placements[i].anchorIndex != anchorIndex)
                    continue;

                if (requireOrientation && _placements[i].orientation != orientation)
                    continue;

                _placements.RemoveAt(i);
                return true;
            }

            return false;
        }
    }
}