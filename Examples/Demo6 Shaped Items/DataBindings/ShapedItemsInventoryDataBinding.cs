using System;
using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.DataBinding;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.UI;

namespace UniversalDragAndDrop.Examples.ShapedItems
{
    [Serializable]
    public struct ShapedPlacementSeed
    {
        public ShapedItemExampleSO item;
        [Min(0)] public int anchorIndex;
        public PlacementOrientation orientation;

        public ShapedPlacementSeed(ShapedItemExampleSO item, int anchorIndex, PlacementOrientation orientation = PlacementOrientation.Rot0)
        {
            this.item = item;
            this.anchorIndex = Mathf.Max(0, anchorIndex);
            this.orientation = orientation;
        }
    }

    public class ShapedItemsInventoryDataBinding : InventoryDataBindingBase
    {
        [Header("Data")]
        [SerializeField] private List<ShapedPlacementSeed> _placements = new List<ShapedPlacementSeed>();

        protected override void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            base.Awake();
        }

        protected override void OnReloadUI()
        {
            if (Inventory == null)
                return;

            if (Inventory.Grid.HasValue)
            {
                ReloadGrid();
            }
            else
            {
                ReloadSlots();
            }

            Inventory.UpdateAllVisuals();
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context?.Stack?.PrimaryAdapter is not ShapedItemAdapter adapter || adapter.item == null)
                return;

            if (Inventory.Grid.HasValue)
            {
                _placements.Add(new ShapedPlacementSeed(
                    adapter.item,
                    Mathf.Max(0, context.AnchorIndex),
                    context.Orientation));
                return;
            }

            _placements.Add(new ShapedPlacementSeed(
                adapter.item,
                Mathf.Max(0, context.SlotIndex),
                PlacementOrientation.Rot0));
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context?.Stack?.PrimaryAdapter is not ShapedItemAdapter adapter || adapter.item == null)
                return;

            RemoveFirstPlacement(adapter.item, context.AnchorIndex);
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

        private void ReloadSlots()
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                var seed = _placements[i];
                if (seed.item == null)
                    continue;

                AddToUIQuiet(new[] { new ShapedItemAdapter(seed.item) });
            }
        }

        private void ReloadGrid()
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                var seed = _placements[i];
                if (seed.item == null)
                    continue;

                var adapter = new ShapedItemAdapter(seed.item);
                if (!ItemStack.TryCreate(new[] { adapter }, out var stack))
                    continue;

                var request = new PlacementRequest(
                    stack,
                    Mathf.Max(0, seed.anchorIndex),
                    seed.orientation,
                    adapter.Footprint);

                if (!Inventory.TryPlace(request))
                {
                    Debug.LogWarning(
                        $"[{nameof(ShapedItemsInventoryDataBinding)}] Could not place '{adapter.DisplayName}' at anchor {seed.anchorIndex}.",
                        this);
                }
            }
        }

        private void RemoveFirstPlacement(ShapedItemExampleSO item, int anchorIndex)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if (!ReferenceEquals(_placements[i].item, item))
                    continue;

                if (anchorIndex >= 0 && _placements[i].anchorIndex != anchorIndex)
                    continue;

                _placements.RemoveAt(i);
                return;
            }

            if (anchorIndex >= 0)
                RemoveFirstPlacement(item, -1);
        }
    }
}
