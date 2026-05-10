using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Tests.Inventories
{
    [TestFixture]
    public class ShapedItemPlacementTests
    {
        [Test]
        public void SlotFacade_MigratesSetStack_ToPlacement()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("sword"));
                var slot = inventory.GetSlot(0);

                slot.SetStack(stack);

                var placement = inventory.GetPlacementAt(0);
                Assert.IsNotNull(placement);
                Assert.AreSame(stack, placement.Stack);
                Assert.AreSame(stack, slot.Stack);
                Assert.IsTrue(inventory.GetSlot(1).IsEmpty);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void InventoryItemEventContext_WithoutPlacementSnapshot_UsesNullAndSafeFallbacks()
        {
            var context = new InventoryItemEventContext(ItemStack.Empty());

            Assert.IsNull(context.PlacementSnapshot);
            Assert.AreEqual(-1, context.AnchorIndex);
            Assert.AreEqual(PlacementOrientation.Rot0, context.Orientation);
            Assert.AreEqual(Footprint.One, context.Footprint);
            Assert.IsEmpty(context.CoveredIndices);
            Assert.IsEmpty(context.CoveredBaseSlots);
        }

        [Test]
        public void InventoryItemEventContext_ResolvedSlots_DoNotCrossFallbackSourceAndTarget()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
                var sourceSlot = inventory.GetSlot(0);
                var targetSlot = inventory.GetSlot(1);
                var snapshot = new PlacementSnapshot(
                    targetSlot.Index,
                    PlacementOrientation.Rot0,
                    Footprint.One,
                    anchorBaseSlot: targetSlot);

                var sourceOnlyContext = new InventoryItemEventContext(stack, sourceBaseSlot: sourceSlot);
                Assert.AreSame(sourceSlot, sourceOnlyContext.ResolvedSourceBaseSlot);
                Assert.IsNull(sourceOnlyContext.ResolvedTargetBaseSlot);

                var targetOnlyContext = new InventoryItemEventContext(stack, targetBaseSlot: targetSlot);
                Assert.AreSame(targetSlot, targetOnlyContext.ResolvedTargetBaseSlot);
                Assert.IsNull(targetOnlyContext.ResolvedSourceBaseSlot);

                var placementContext = new InventoryItemEventContext(stack, placementSnapshot: snapshot);
                Assert.AreSame(targetSlot, placementContext.ResolvedTargetBaseSlot);
                Assert.AreSame(targetSlot, placementContext.ResolvedSourceBaseSlot);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void SlotFacade_SetStackOnCoveredNonAnchor_DoesNotReplacePlacement()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                var coveredSlot = inventory.GetSlot(3);
                LogAssert.Expect(
                    LogType.Warning,
                    $"[{coveredSlot.name}] SetStack failed for placement-backed slot {coveredSlot.Index}");

                coveredSlot.SetStack(ItemStackBuilder.Of(new FakeItemAdapter("gem")));

                Assert.AreSame(placement, inventory.GetPlacementAt(0));
                Assert.AreSame(placement, inventory.GetPlacementAt(3));
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, placement.CoveredIndices);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void SlotFacade_FailedSetStack_RestoresExistingPlacement()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .Build();

            try
            {
                var original = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
                var blocker = ItemStackBuilder.Of(new FakeItemAdapter("rock"));
                inventory.GetSlot(0).SetStack(original);
                inventory.GetSlot(1).SetStack(blocker);

                var anchorSlot = inventory.GetSlot(0);
                LogAssert.Expect(
                    LogType.Warning,
                    $"[{anchorSlot.name}] SetStack failed for placement-backed slot {anchorSlot.Index}");

                anchorSlot.SetStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2)));

                Assert.AreSame(original, inventory.GetSlot(0).Stack);
                Assert.AreSame(blocker, inventory.GetSlot(1).Stack);
                Assert.IsNull(inventory.GetPlacementAt(2));
                Assert.IsNull(inventory.GetPlacementAt(3));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void TryPlace_WithGridTopology_CoversFootprintCells()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));

                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, placement.CoveredIndices);
                Assert.AreSame(placement, inventory.GetPlacementAt(4));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void Footprint_GetSize_SupportsFourOrientations()
        {
            var footprint = new Footprint(2, 3);

            Assert.AreEqual(new Vector2Int(2, 3), footprint.GetSize(PlacementOrientation.Rot0));
            Assert.AreEqual(new Vector2Int(3, 2), footprint.GetSize(PlacementOrientation.Rot90));
            Assert.AreEqual(new Vector2Int(2, 3), footprint.GetSize(PlacementOrientation.Rot180));
            Assert.AreEqual(new Vector2Int(3, 2), footprint.GetSize(PlacementOrientation.Rot270));
        }

        [Test]
        public void DragEntry_FromCoveredCell_CapturesPlacementSnapshot()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                var coveredSlot = inventory.GetSlot(4);
                var entry = new DragEntry(stack.CreateCopy(), coveredSlot, inventory);

                Assert.AreSame(placement, entry.SourcePlacement);
                Assert.AreEqual(new Vector2Int(1, 1), entry.GrabOffset);
                Assert.AreEqual(new Footprint(2, 2), entry.Footprint);
                Assert.AreEqual(PlacementOrientation.Rot0, entry.Orientation);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void DragEntry_WithOrientation_RotatesGrabOffset()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("blade", 2, 1));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                var entry = new DragEntry(stack.CreateCopy(), inventory.GetSlot(1), inventory, placement);
                var rotated = entry.WithOrientation(PlacementOrientation.Rot90);

                Assert.AreEqual(PlacementOrientation.Rot90, rotated.Orientation);
                Assert.AreEqual(new Vector2Int(0, 1), rotated.GrabOffset);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void DragEntry_DirectOrientation_RotatesResolvedGrabOffset()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("blade", 2, 1));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                var entry = new DragEntry(
                    stack.CreateCopy(),
                    inventory.GetSlot(1),
                    inventory,
                    placement,
                    orientation: PlacementOrientation.Rot90);

                Assert.AreEqual(PlacementOrientation.Rot90, entry.Orientation);
                Assert.AreEqual(new Vector2Int(0, 1), entry.GrabOffset);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void DropPreview_UsesCoveredCellsFromGrabOffset()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(4);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                Assert.IsTrue(target.TryGetDropPreviewSlots(
                    target.GetSlot(4),
                    context,
                    out var previewSlots,
                    out bool canPlace));

                Assert.IsTrue(canPlace);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 3, 4 },
                    previewSlots.Select(slot => slot.Index).ToArray());
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void DropPreview_SourceGrabOffsetAnchorStrategy_DoesNotShiftSquareWhenRotated()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(9)
                .WithGridTopology(3, 3)
                .Build();

            try
            {
                inventory.SetShapedPlacementAnchorStrategy(new SourceGrabOffsetAnchorStrategy());
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = inventory.GetSlot(4);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, inventory)
                    .WithOrientation(PlacementOrientation.Rot90);
                var context = new DragContext(new[] { entry });

                Assert.IsTrue(inventory.TryGetDropPreviewSlots(
                    inventory.GetSlot(4),
                    context,
                    out var previewSlots,
                    out bool canPlace));

                Assert.IsTrue(canPlace);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 3, 4 },
                    previewSlots.Select(slot => slot.Index).ToArray());
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void DropPreview_WhenFootprintLeavesGrid_ShowsInBoundsCells()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                Assert.IsTrue(target.TryGetDropPreviewSlots(
                    target.GetSlot(2),
                    context,
                    out var previewSlots,
                    out bool canPlace));

                Assert.IsFalse(canPlace);
                CollectionAssert.AreEqual(
                    new[] { 2, 5 },
                    previewSlots.Select(slot => slot.Index).ToArray());
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void DropPreview_WhenFootprintLeavesGridLeft_ShowsInBoundsCells()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(1);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                Assert.IsTrue(target.TryGetDropPreviewSlots(
                    target.GetSlot(0),
                    context,
                    out var previewSlots,
                    out bool canPlace));

                Assert.IsFalse(canPlace);
                CollectionAssert.AreEqual(
                    new[] { 0, 3 },
                    previewSlots.Select(slot => slot.Index).ToArray());
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void DropPreview_WhenFootprintLeavesGridUp_ShowsInBoundsCells()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(3);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                Assert.IsTrue(target.TryGetDropPreviewSlots(
                    target.GetSlot(0),
                    context,
                    out var previewSlots,
                    out bool canPlace));

                Assert.IsFalse(canPlace);
                CollectionAssert.AreEqual(
                    new[] { 0, 1 },
                    previewSlots.Select(slot => slot.Index).ToArray());
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_UsesDragEntryOrientation()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .WithName("SourceGrid")
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(9)
                .WithGridTopology(3, 3)
                .WithName("TargetGrid")
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("blade", 2, 1));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(
                    dragSlot.Stack.CreateCopy(),
                    dragSlot,
                    source,
                    orientation: PlacementOrientation.Rot90);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(1), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                var targetPlacement = target.GetPlacementAt(1);
                Assert.IsNotNull(targetPlacement);
                Assert.AreEqual(PlacementOrientation.Rot90, targetPlacement.Orientation);
                CollectionAssert.AreEqual(new[] { 1, 4 }, targetPlacement.CoveredIndices);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void BuildPlan_ShapedGridPlacement_UsesPlacementAllocation()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(9)
                .WithGridTopology(3, 3)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(4);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });
                var planner = new TransferPlanner();

                var plan = planner.BuildPlan(
                    context,
                    new DropPolicySettings().Resolve(null, context),
                    target,
                    target.GetSlot(8),
                    new GlobalRuleValidator());

                Assert.IsTrue(plan.IsValid, plan.Failure?.Reason);
                Assert.AreEqual(1, plan.Entries.Count);
                Assert.IsTrue(plan.Entries[0].HasPlacementAllocation);
                Assert.AreEqual(0, plan.Entries[0].Allocations.Count);

                var allocation = plan.Entries[0].PlacementAllocation.Value;
                Assert.AreEqual(4, allocation.AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot0, allocation.Orientation);
                Assert.AreEqual(new Footprint(2, 2), allocation.Footprint);
                Assert.AreEqual(1, allocation.Amount);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void DragContext_ShapedBatchAndStackedShapedEntries_AreDetected()
        {
            var shapedStack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1));
            var stackedShapedStack = ItemStackBuilder.Of(
                new FootprintAdapter("crate", 2, 1),
                new FootprintAdapter("crate", 2, 1));

            var batchContext = new DragContext(new[]
            {
                new DragEntry(shapedStack, null, null),
                new DragEntry(ItemStackBuilder.Of(new FakeItemAdapter("coin")), null, null)
            });
            var stackedContext = new DragContext(new[]
            {
                new DragEntry(stackedShapedStack, null, null)
            });

            Assert.IsTrue(batchContext.IsBatchDrag);
            Assert.IsTrue(batchContext.HasShapedEntries);
            Assert.IsTrue(stackedContext.HasStackedShapedEntries);
        }

        [Test]
        public void AutoTransferService_RejectsShapedItems()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(4)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var service = new AutoTransferService();
                Assert.IsFalse(service.TryCreateContext(
                    new[] { source.GetSlot(0) },
                    source,
                    target,
                    out var context,
                    out string failureReason));
                Assert.IsNull(context);
                StringAssert.Contains("shaped", failureReason);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void StackableSlotInventory_ShapedItemsDoNotMerge()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .Build();

            try
            {
                Assert.IsTrue(inventory.TryAddStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1))));
                Assert.IsTrue(inventory.TryAddStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1))));

                Assert.AreEqual(1, inventory.GetSlot(0).Stack.Count);
                Assert.AreEqual(1, inventory.GetSlot(1).Stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void ProcessDrop_ShapedSlotToOccupiedSlot_Rejects()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();

            try
            {
                Assert.IsTrue(source.TryAddStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1))));
                Assert.IsTrue(target.TryAddStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1))));

                var sourceSlot = source.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsFalse(summary.Success);
                Assert.AreEqual(1, source.GetSlot(0).Stack.Count);
                Assert.AreEqual(1, target.GetSlot(0).Stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_UsesGrabOffsetAnchor()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .WithName("SourceGrid")
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .WithName("TargetGrid")
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(4);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(4), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(1, summary.TransferredAmount);
                Assert.IsNull(source.GetPlacementAt(0));

                var targetPlacement = target.GetPlacementAt(4);
                Assert.IsNotNull(targetPlacement);
                Assert.AreEqual(0, targetPlacement.AnchorIndex);
                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, targetPlacement.CoveredIndices);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_ReportsPlacementSnapshot()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .WithName("SourceGrid")
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(9)
                .WithGridTopology(3, 3)
                .WithName("TargetGrid")
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                InventoryItemEventContext removedContext = null;
                InventoryItemEventContext addedContext = null;
                source.OnItemRemoved += context => removedContext = context;
                target.OnItemAdded += context => addedContext = context;

                var dragSlot = source.GetSlot(4);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(8), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                Assert.AreEqual(4, summary.DropResult.AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot0, summary.DropResult.Orientation);
                Assert.AreEqual(new Footprint(2, 2), summary.DropResult.Footprint);
                CollectionAssert.AreEqual(new[] { 4, 5, 7, 8 }, summary.DropResult.CoveredIndices);
                CollectionAssert.AreEqual(
                    new[] { 4, 5, 7, 8 },
                    summary.DropResult.CoveredSlots.Select(slot => slot.Index).ToArray());

                Assert.AreEqual(1, summary.ExecutedEntries.Count);
                var executedEntry = summary.ExecutedEntries[0];
                Assert.AreEqual(4, executedEntry.AnchorIndex);
                CollectionAssert.AreEqual(new[] { 4, 5, 7, 8 }, executedEntry.CoveredIndices);
                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, executedEntry.SourcePlacementSnapshot.CoveredIndices);

                Assert.IsNotNull(removedContext);
                Assert.AreEqual(0, removedContext.AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot0, removedContext.Orientation);
                Assert.AreEqual(new Footprint(2, 2), removedContext.Footprint);
                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, removedContext.CoveredIndices);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 3, 4 },
                    removedContext.CoveredBaseSlots.Select(slot => slot.Index).ToArray());

                Assert.IsNotNull(addedContext);
                Assert.AreEqual(4, addedContext.AnchorIndex);
                Assert.AreSame(target.GetSlot(4), addedContext.AnchorBaseSlot);
                Assert.AreEqual(PlacementOrientation.Rot0, addedContext.Orientation);
                Assert.AreEqual(new Footprint(2, 2), addedContext.Footprint);
                CollectionAssert.AreEqual(new[] { 4, 5, 7, 8 }, addedContext.CoveredIndices);
                CollectionAssert.AreEqual(
                    new[] { 4, 5, 7, 8 },
                    addedContext.CoveredBaseSlots.Select(slot => slot.Index).ToArray());
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedWithinSameGrid_AllowsOverlapWithSourcePlacement()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = inventory.GetSlot(4);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, inventory);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(inventory.GetSlot(5), inventory, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                var movedPlacement = inventory.GetPlacementAt(5);
                Assert.IsNotNull(movedPlacement);
                Assert.AreEqual(1, movedPlacement.AnchorIndex);
                CollectionAssert.AreEqual(new[] { 1, 2, 4, 5 }, movedPlacement.CoveredIndices);
                Assert.IsNull(inventory.GetPlacementAt(0));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void ProcessDrop_RotatedShapedWithinSameGrid_DraggedFromLowerCell_AllowsMoveDown()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(9)
                .WithGridTopology(3, 3)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("blade", 2, 1));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0, PlacementOrientation.Rot90)));

                var dragSlot = inventory.GetSlot(3);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, inventory)
                    .WithOrientation(PlacementOrientation.Rot180);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(inventory.GetSlot(6), inventory, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                var movedPlacement = inventory.GetPlacementAt(6);
                Assert.IsNotNull(movedPlacement);
                Assert.AreEqual(6, movedPlacement.AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot180, movedPlacement.Orientation);
                CollectionAssert.AreEqual(new[] { 6, 7 }, movedPlacement.CoveredIndices);
                Assert.IsNull(inventory.GetPlacementAt(0));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void ProcessDrop_RotatedShapedWithinSameGrid_AllowsRotationBeforeLeavingSourceArea()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(9)
                .WithGridTopology(3, 3)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("blade", 2, 1));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0, PlacementOrientation.Rot90)));

                var dragSlot = inventory.GetSlot(3);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, inventory)
                    .WithOrientation(PlacementOrientation.Rot180);
                var context = new DragContext(new[] { entry });

                var globalRules = new GlobalRuleValidator();
                globalRules.AddRule(new SameSlotRule());
                var processor = new InventoryDropProcessor(inventory.GetSlot(3), inventory, globalRules);
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                var movedPlacement = inventory.GetPlacementAt(3);
                Assert.IsNotNull(movedPlacement);
                Assert.AreEqual(3, movedPlacement.AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot180, movedPlacement.Orientation);
                CollectionAssert.AreEqual(new[] { 3, 4 }, movedPlacement.CoveredIndices);
                Assert.IsNull(inventory.GetPlacementAt(0));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void CanPlace_RejectsOverlapAndOutOfBoundsFootprints()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var shapedStack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(shapedStack, 0)));

                var overlappingStack = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(overlappingStack, 1)));

                var outOfBoundsStack = ItemStackBuilder.Of(new FootprintAdapter("shield", 2, 2));
                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(outOfBoundsStack, 2)));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void CanPlace_RejectsMultiCellStackWithMultipleItems()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var stack = ItemStackBuilder.Of(
                    new FootprintAdapter("bag", 2, 1),
                    new FootprintAdapter("bag", 2, 1));

                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(stack, 0)));
                Assert.IsFalse(inventory.TryAddStack(stack));
                Assert.AreEqual(2, stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void GridInventory_LegacyTryAddStackRejectsShapedItems()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1));

                Assert.IsFalse(inventory.TryAddStack(stack));
                Assert.AreEqual(1, stack.Count);
                Assert.IsNull(inventory.GetPlacementAt(0));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void SlotInventory_DefaultPolicyAcceptsShapedAsSingleSlot()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));

                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                CollectionAssert.AreEqual(new[] { 0 }, placement.CoveredIndices);
                Assert.AreSame(placement, inventory.GetPlacementAt(0));
                Assert.IsNull(inventory.GetPlacementAt(1));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void SlotInventory_RejectPolicyRejectsShapedItems()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .WithSlotShapedItemPolicy(SlotShapedItemPolicy.Reject)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));

                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(stack, 0)));
                Assert.IsFalse(inventory.TryPlace(new PlacementRequest(stack, 0)));
                Assert.IsFalse(inventory.CanAcceptItem(stack.PrimaryAdapter, stack.Count, out _));
                Assert.IsFalse(inventory.TryAddStack(stack));
                Assert.AreEqual(1, stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void GridInventory_ReplacesDynamicSlotManagementWithFixed()
        {
            LogAssert.Expect(
                LogType.Error,
                "[TestInventory] Grid placement requires fixed slot management. Dynamic slot management was replaced with FixedSlotManagementSettings.");

            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .WithSlotManagementSettings(new DynamicSlotManagementSettings())
                .Build();

            try
            {
                Assert.IsInstanceOf<StackableItemStrategy>(inventory.Strategy);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void CanPlace_PrunesPlacementAfterStackMutationEmptiesIt()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("potion"));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0)));

                stack.RemoveFromStack(1);

                var replacement = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
                Assert.IsTrue(inventory.CanPlace(new PlacementRequest(replacement, 0)));
                Assert.IsNull(inventory.GetPlacementAt(0));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void Snapshot_PreservesPlacementSnapshot()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var originalPlacement));

                var snapshot = inventory.CaptureSnapshot();

                Assert.AreEqual(1, snapshot.Placements.Count);
                Assert.AreEqual(0, snapshot.Placements[0].AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot0, snapshot.Placements[0].Orientation);
                Assert.AreEqual(new Footprint(2, 2), snapshot.Placements[0].Footprint);
                CollectionAssert.AreEqual(originalPlacement.CoveredIndices, snapshot.Placements[0].CoveredIndices);

                Assert.IsTrue(inventory.RemovePlacementAt(0));
                inventory.RestoreSnapshot(snapshot);

                var restoredPlacement = inventory.GetPlacementAt(4);
                Assert.IsNotNull(restoredPlacement);
                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, restoredPlacement.CoveredIndices);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void InventorySnapshot_CopiesPlacementList()
        {
            var adapters = ItemStackBuilder.Of(new FakeItemAdapter("gem")).Adapters;
            var placements = new System.Collections.Generic.List<InventoryPlacementState>
            {
                new InventoryPlacementState(
                    0,
                    adapters,
                    PlacementOrientation.Rot0,
                    Footprint.One,
                    new[] { 0 })
            };

            var snapshot = new InventorySnapshot(2, placements);

            placements.Clear();

            Assert.AreEqual(1, snapshot.Placements.Count);
            Assert.AreEqual(0, snapshot.Placements[0].AnchorIndex);
        }

        [Test]
        public void RestoreSnapshot_WhenPlacementCannotBeRestored_LogsError()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();

            try
            {
                EnableGrid(inventory, 1, 1);
                var stack = ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1));
                var snapshot = new InventorySnapshot(
                    1,
                    new System.Collections.Generic.List<InventoryPlacementState>
                    {
                        new InventoryPlacementState(
                            0,
                            stack.Adapters,
                            PlacementOrientation.Rot0,
                            new Footprint(2, 1),
                            new[] { 0, 1 })
                    });

                LogAssert.Expect(
                    LogType.Error,
                    $"[{nameof(UniversalInventory)}] Failed to restore placement at anchor 0 (2x1, Rot0).");

                inventory.RestoreSnapshot(snapshot);

                Assert.IsNull(inventory.GetPlacementAt(0));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void RestoreSnapshot_WhenPlacementCannotBeRestored_DoesNotApplyPartialSnapshot()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .Build();

            try
            {
                EnableGrid(inventory, 2, 2);
                var existingAdapter = new FakeItemAdapter("existing");
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(ItemStackBuilder.Of(existingAdapter), 3)));

                var firstSnapshotStack = ItemStackBuilder.Of(new FakeItemAdapter("first"));
                var overlappingSnapshotStack = ItemStackBuilder.Of(new FakeItemAdapter("overlap"));
                var snapshot = new InventorySnapshot(
                    4,
                    new System.Collections.Generic.List<InventoryPlacementState>
                    {
                        new InventoryPlacementState(
                            0,
                            firstSnapshotStack.Adapters,
                            PlacementOrientation.Rot0,
                            Footprint.One,
                            new[] { 0 }),
                        new InventoryPlacementState(
                            0,
                            overlappingSnapshotStack.Adapters,
                            PlacementOrientation.Rot0,
                            Footprint.One,
                            new[] { 0 })
                    });

                LogAssert.Expect(
                    LogType.Error,
                    $"[{nameof(UniversalInventory)}] Failed to restore placement at anchor 0 (1x1, Rot0).");

                inventory.RestoreSnapshot(snapshot);

                Assert.IsNull(inventory.GetPlacementAt(0));
                var existingPlacement = inventory.GetPlacementAt(3);
                Assert.IsNotNull(existingPlacement);
                Assert.AreSame(existingAdapter, existingPlacement.Stack.PrimaryAdapter);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void TryPlanSwapAgainstTarget_ShapedSourceItem_IsRejected()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();

            try
            {
                Assert.IsTrue(source.TryAddStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1))));
                Assert.IsTrue(target.TryAddStack(ItemStackBuilder.Of(new FakeItemAdapter("coin"))));

                var sourceSlot = source.GetSlot(0);
                var targetSlot = target.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                var context = new DragContext(new[] { entry });

                var planner = new TransferPlanner();
                var planned = InvokeTryPlanSwapAgainstTarget(
                    planner,
                    context,
                    entry,
                    target,
                    targetSlot,
                    requested: 1,
                    targetItem: entry.Stack.PrimaryAdapter);

                Assert.IsNull(planned, "Shaped source must not produce a swap plan");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void TryPlanSwapAgainstTarget_ShapedTargetItem_IsRejected()
        {
            var source = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();
            var target = new InventoryBuilder()
                .WithFixedSlots(1)
                .Build();

            try
            {
                Assert.IsTrue(source.TryAddStack(ItemStackBuilder.Of(new FakeItemAdapter("coin"))));
                Assert.IsTrue(target.TryAddStack(ItemStackBuilder.Of(new FootprintAdapter("bag", 2, 1))));

                var sourceSlot = source.GetSlot(0);
                var targetSlot = target.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                var context = new DragContext(new[] { entry });

                var planner = new TransferPlanner();
                var planned = InvokeTryPlanSwapAgainstTarget(
                    planner,
                    context,
                    entry,
                    target,
                    targetSlot,
                    requested: 1,
                    targetItem: entry.Stack.PrimaryAdapter);

                Assert.IsNull(planned, "Shaped target must not be swapped");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        private static PlannedEntryTransfer InvokeTryPlanSwapAgainstTarget(
            TransferPlanner planner,
            DragContext context,
            DragEntry entry,
            UniversalInventory targetInventory,
            BaseSlot targetSlot,
            int requested,
            IItemAdapter targetItem)
        {
            var method = typeof(TransferPlanner).GetMethod(
                "TryPlanSwapAgainstTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TryPlanSwapAgainstTarget must exist");
            return (PlannedEntryTransfer)method.Invoke(
                planner,
                new object[]
                {
                    context, entry, targetInventory, targetSlot,
                    new GlobalRuleValidator(), requested, targetItem
                });
        }

        private static void EnableGrid(UniversalInventory inventory, int columns, int rows)
        {
            SetField(inventory, "_useGridTopology", true);
            SetField(inventory, "_gridTopology", new GridTopology(columns, rows));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} must exist");
            field.SetValue(target, value);
        }

        private sealed class FootprintAdapter : IItemAdapter, IItemFootprintProvider
        {
            public FootprintAdapter(string itemId, int width, int height)
            {
                ItemId = itemId;
                DisplayName = itemId;
                Footprint = new Footprint(width, height);
            }

            public string ItemId { get; }
            public string DisplayName { get; }
            public Sprite Icon => null;
            public Footprint Footprint { get; }
        }
    }
}
