using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Tests.Inventories
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
                Assert.AreNotSame(stack, placement.Stack);
                Assert.AreSame(placement.Stack, slot.Stack);
                Assert.AreEqual(stack.PrimaryAdapter, placement.Stack.PrimaryAdapter);
                Assert.Throws<System.NotSupportedException>(() =>
                    ((System.Collections.Generic.IList<int>)placement.CoveredIndices)[0] = 1);
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
            Assert.AreEqual(Vector2Int.one, context.BoundingSize);
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
                    Vector2Int.one,
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
        public void SlotStore_SetStackOnCoveredNonAnchor_DoesNotReplacePlacement()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                var coveredSlot = inventory.GetSlot(3);
                Assert.IsFalse(inventory.TrySetStackForSlot(coveredSlot, ItemStackBuilder.Of(new FakeItemAdapter("gem"))));

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
        public void SlotStore_FailedSetStack_RestoresExistingPlacement()
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
                Assert.IsFalse(inventory.TrySetStackForSlot(anchorSlot, ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2))));

                Assert.AreEqual(original.PrimaryAdapter, inventory.GetSlot(0).Stack.PrimaryAdapter);
                Assert.AreEqual(blocker.PrimaryAdapter, inventory.GetSlot(1).Stack.PrimaryAdapter);
                Assert.IsNull(inventory.GetPlacementAt(2));
                Assert.IsNull(inventory.GetPlacementAt(3));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void DynamicSlotRemoval_RecreatesShiftedPlacementsWithoutMutatingOldReference()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(1)
                .WithSlotManagementSettings(new DynamicSlotManagementSettings())
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
                Assert.IsTrue(inventory.TryAddStack(stack, targetSlotIndex: 2));
                var originalPlacement = inventory.GetPlacementAt(2);
                Assert.IsNotNull(originalPlacement);
                Assert.AreEqual(2, originalPlacement.AnchorIndex);

                var emptySlot = inventory.GetSlot(0);
                inventory.HandleSlotEmptied(emptySlot);

                Assert.AreEqual(2, originalPlacement.AnchorIndex);
                var shiftedPlacement = inventory.GetPlacementAt(1);
                Assert.IsNotNull(shiftedPlacement);
                Assert.AreNotSame(originalPlacement, shiftedPlacement);
                Assert.AreEqual(1, shiftedPlacement.AnchorIndex);
                Assert.AreSame(originalPlacement.Stack.PrimaryAdapter, shiftedPlacement.Stack.PrimaryAdapter);
                Assert.AreEqual(2, inventory.SlotCount, "Only the reported empty slot should be removed in one HandleSlotEmptied call");
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void DynamicSlotRemoval_DuringActiveShapedDrag_CancelDoesNotThrow()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(1)
                .WithSlotManagementSettings(new DynamicSlotManagementSettings())
                .Build();
            UDND.DragAndDropManager manager = null;

            try
            {
                var shapedStack = ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1));
                Assert.IsTrue(inventory.TryAddStack(shapedStack, targetSlotIndex: 2));

                manager = UDND.DragAndDropManager.AutoCreateInstance;
                Assert.IsTrue(manager.StartDrag(inventory.GetSlot(2)));
                Assert.IsNotNull(manager.CurrentContext);
                Assert.IsNotNull(manager.CurrentContext.Entries[0].SourcePlacement);

                inventory.HandleSlotEmptied(inventory.GetSlot(0));

                Assert.DoesNotThrow(() => manager.CancelDrag());
                Assert.IsNull(manager.CurrentContext);
                Assert.IsFalse(inventory.GetSlot(1).IsDraggedFromVisualState);
            }
            finally
            {
                if (manager != null)
                    UnityEngine.Object.DestroyImmediate(manager.gameObject);
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void TryPlace_WithGridTopology_CoversShapeSizeCells()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));

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
        public void RectPlacementShape_GetBoundingSize_SupportsFourOrientations()
        {
            var shape = new RectPlacementShape(2, 3);

            Assert.AreEqual(new Vector2Int(2, 3), PlacementShapeUtility.GetBoundingSize(shape, PlacementOrientation.Rot0));
            Assert.AreEqual(new Vector2Int(3, 2), PlacementShapeUtility.GetBoundingSize(shape, PlacementOrientation.Rot90));
            Assert.AreEqual(new Vector2Int(2, 3), PlacementShapeUtility.GetBoundingSize(shape, PlacementOrientation.Rot180));
            Assert.AreEqual(new Vector2Int(3, 2), PlacementShapeUtility.GetBoundingSize(shape, PlacementOrientation.Rot270));
        }

        [Test]
        public void PlacementCellUtility_RectGridRequireAllInBounds_MatchesLegacyCoveredCells()
        {
            var topologies = new[]
            {
                new GridTopology(1, 1),
                new GridTopology(2, 2),
                new GridTopology(3, 2),
                new GridTopology(5, 4)
            };
            var sizes = new[]
            {
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(1, 2),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2)
            };
            var orientations = new[]
            {
                PlacementOrientation.Rot0,
                PlacementOrientation.Rot90,
                PlacementOrientation.Rot180,
                PlacementOrientation.Rot270
            };

            foreach (var topology in topologies)
            {
                int slotCount = topology.CellCount;
                for (int anchorIndex = -1; anchorIndex <= slotCount; anchorIndex++)
                {
                    foreach (var size in sizes)
                    {
                        foreach (var orientation in orientations)
                        {
                            var expected = LegacyRequireAllCoveredCells(anchorIndex, size, orientation, topology, slotCount);
                            var actual = PlacementCellUtility.GetCoveredIndices(
                                anchorIndex,
                                new RectPlacementShape(size.x, size.y),
                                orientation,
                                new RectGridTopology(topology),
                                PlacementBoundsMode.RequireAllInBounds);

                            CollectionAssert.AreEqual(expected, actual);
                        }
                    }
                }
            }
        }

        [Test]
        public void PlacementCellUtility_RectGridIncludeOnlyInBounds_MatchesLegacyPreviewCells()
        {
            var topology = new GridTopology(3, 2);
            int slotCount = topology.CellCount;
            var anchors = new[]
            {
                new Vector2Int(-1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 1),
                new Vector2Int(3, 1)
            };
            var sizes = new[]
            {
                new Vector2Int(1, 1),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2)
            };
            var orientations = new[]
            {
                PlacementOrientation.Rot0,
                PlacementOrientation.Rot90,
                PlacementOrientation.Rot180,
                PlacementOrientation.Rot270
            };

            foreach (var anchorCell in anchors)
            {
                foreach (var size in sizes)
                {
                    foreach (var orientation in orientations)
                    {
                        var expected = LegacyPreviewCoveredCells(anchorCell, size, orientation, topology, slotCount);
                        var actual = PlacementCellUtility.GetCoveredIndices(
                            anchorCell,
                            new RectPlacementShape(size.x, size.y),
                            orientation,
                            new RectGridTopology(topology),
                            PlacementBoundsMode.IncludeOnlyInBounds);

                        CollectionAssert.AreEqual(expected, actual);
                    }
                }
            }
        }

        [Test]
        public void RectGridTopology_TryToIndex_MatchesGridTopology()
        {
            var grid = new GridTopology(3, 2);
            IInventoryTopology topology = new RectGridTopology(grid);

            for (int index = 0; index < grid.CellCount; index++)
            {
                var cell = grid.ToCell(index);

                Assert.IsTrue(topology.TryToIndex(cell, out int resolvedIndex));
                Assert.AreEqual(index, resolvedIndex);
                Assert.AreEqual(cell, topology.ToCell(index));
            }

            Assert.IsFalse(topology.TryToIndex(new Vector2Int(-1, 0), out _));
            Assert.IsFalse(topology.TryToIndex(new Vector2Int(3, 0), out _));
            Assert.IsFalse(topology.TryToIndex(new Vector2Int(0, 2), out _));
        }

        [Test]
        public void SlotTopology_TryToIndex_UsesOneDimensionalCells()
        {
            IInventoryTopology topology = new SlotTopology(3);

            Assert.IsTrue(topology.TryToIndex(new Vector2Int(0, 0), out int firstIndex));
            Assert.AreEqual(0, firstIndex);
            Assert.IsTrue(topology.TryToIndex(new Vector2Int(2, 0), out int lastIndex));
            Assert.AreEqual(2, lastIndex);
            Assert.IsFalse(topology.TryToIndex(new Vector2Int(3, 0), out _));
            Assert.IsFalse(topology.TryToIndex(new Vector2Int(0, 1), out _));
        }

        [Test]
        public void SlotTopology_GetPlacementOffsets_UsesAnchorForAnyShape()
        {
            IInventoryTopology topology = new SlotTopology(3);
            var shape = new TestPlacementShape(
                Vector2Int.zero,
                Vector2Int.right,
                Vector2Int.up);

            var offsets = topology.GetPlacementOffsets(shape, PlacementOrientation.Rot90);

            CollectionAssert.AreEqual(new[] { Vector2Int.zero }, offsets);
        }

        [Test]
        public void PlacementCellUtility_WithTopology_UsesTopologyBounds()
        {
            IInventoryTopology topology = new RectGridTopology(3, 2);
            var shape = new TestPlacementShape(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1));

            var covered = PlacementCellUtility.GetCoveredIndices(
                0,
                shape,
                PlacementOrientation.Rot0,
                topology,
                PlacementBoundsMode.RequireAllInBounds);

            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, covered);
        }

        [Test]
        public void PlacementStore_RectGrid_RegistersAllCoveredCells()
        {
            var store = new PlacementStore(new RectGridTopology(3, 2));
            var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));

            Assert.IsTrue(store.TryPlace(new PlacementRequest(stack, 0), out var placement));

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, placement.CoveredIndices);
            Assert.AreSame(placement, store.GetAt(4));
            Assert.IsFalse(store.CanPlace(new PlacementRequest(ItemStackBuilder.Of(new FakeItemAdapter("gem")), 4)));
        }

        [Test]
        public void PlacementStore_SlotTopology_UsesOneSlotForShapedPlacement()
        {
            var store = new PlacementStore(new SlotTopology(4));
            var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));

            Assert.IsTrue(store.TryPlace(new PlacementRequest(stack, 1), out var placement));

            CollectionAssert.AreEqual(new[] { 1 }, placement.CoveredIndices);
            Assert.AreSame(placement, store.GetAt(1));
            Assert.IsNull(store.GetAt(2));
        }

        [Test]
        public void PlacementStore_RectGrid_RejectsOutOfBoundsAndUnsupportedOrientation()
        {
            var store = new PlacementStore(new RectGridTopology(2, 2));
            var outOfBoundsStack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
            var unsupportedShape = new SingleOrientationShape(PlacementOrientation.Rot0, Vector2Int.zero);
            var unsupportedStack = ItemStackBuilder.Of(new FakeItemAdapter("gem"));

            Assert.IsFalse(store.TryPlace(new PlacementRequest(outOfBoundsStack, 3), out _));
            Assert.IsFalse(store.TryPlace(
                new PlacementRequest(
                    unsupportedStack,
                    0,
                    PlacementOrientation.Rot90,
                    unsupportedShape),
                out _));
        }

        [Test]
        public void PlacementStore_Remove_UnregistersAllCoveredCells()
        {
            var store = new PlacementStore(new RectGridTopology(3, 2));
            var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
            Assert.IsTrue(store.TryPlace(new PlacementRequest(stack, 0), out var placement));

            Assert.IsTrue(store.Remove(placement));

            Assert.IsNull(store.GetAt(0));
            Assert.IsNull(store.GetAt(1));
            Assert.IsNull(store.GetAt(3));
            Assert.IsNull(store.GetAt(4));
        }

        [Test]
        public void PlacementStore_ShiftAfterSlotRemoved_RecreatesPlacements()
        {
            var store = new PlacementStore(new SlotTopology(3));
            var stack = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
            Assert.IsTrue(store.TryPlace(new PlacementRequest(stack, 2), out var originalPlacement));

            store.ShiftAfterSlotRemoved(0);

            Assert.AreEqual(2, originalPlacement.AnchorIndex);
            Assert.IsNull(store.GetAt(2));
            var shiftedPlacement = store.GetAt(1);
            Assert.IsNotNull(shiftedPlacement);
            Assert.AreNotSame(originalPlacement, shiftedPlacement);
            Assert.AreEqual(1, shiftedPlacement.AnchorIndex);
            Assert.AreSame(originalPlacement.Stack.PrimaryAdapter, shiftedPlacement.Stack.PrimaryAdapter);
        }

        [Test]
        public void PlacementShapeUtility_Resolve_UsesShapeProvider()
        {
            var adapter = new ShapeAdapter("hybrid", 3, 1);

            var shape = PlacementShapeUtility.Resolve(adapter);

            Assert.AreSame(adapter.PlacementShape, shape);
            Assert.AreEqual(new Vector2Int(3, 1), PlacementShapeUtility.GetBoundingSize(shape, PlacementOrientation.Rot0));
        }

        [Test]
        public void PlacementShapeUtility_Resolve_FallsBackToSingleCell()
        {
            var adapter = new FakeItemAdapter("bag");

            var shape = PlacementShapeUtility.Resolve(adapter);

            Assert.IsInstanceOf<RectPlacementShape>(shape);
            Assert.AreEqual(Vector2Int.one, PlacementShapeUtility.GetBoundingSize(shape, PlacementOrientation.Rot0));
        }

        [Test]
        public void RectPlacementShape_GetOffsets_ReturnsCachedOrientationLists()
        {
            var shape = new RectPlacementShape(2, 3);

            Assert.AreSame(
                shape.GetOffsets(PlacementOrientation.Rot0),
                shape.GetOffsets(PlacementOrientation.Rot0));
            Assert.AreSame(
                shape.GetOffsets(PlacementOrientation.Rot90),
                shape.GetOffsets(PlacementOrientation.Rot90));
        }

        [Test]
        public void TryPlace_WithExplicitPlacementShape_UsesShapeOffsets()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var shape = new TestPlacementShape(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1));
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("tool"));

                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0, PlacementOrientation.Rot0, shape), out var placement));

                Assert.AreSame(shape, placement.Shape);
                CollectionAssert.AreEqual(new[] { 0, 1, 3 }, placement.CoveredIndices);
                Assert.AreSame(placement, inventory.GetPlacementAt(3));
                Assert.IsNull(inventory.GetPlacementAt(4));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void CanPlace_WithUnsupportedShapeOrientation_ReturnsFalse()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(2, 2)
                .Build();

            try
            {
                var shape = new SingleOrientationShape(PlacementOrientation.Rot0, Vector2Int.zero, new Vector2Int(1, 0));
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("tool"));

                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(stack, 0, PlacementOrientation.Rot90, shape)));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void PlacementSnapshot_FromPlacement_CapturesShapeOffsetsAndBounds()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var shape = new TestPlacementShape(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1));
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("tool"));

                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0, PlacementOrientation.Rot0, shape), out var placement));

                var snapshot = PlacementSnapshot.FromPlacement(placement, inventory.GetSlot);

                CollectionAssert.AreEqual(shape.GetOffsets(PlacementOrientation.Rot0), snapshot.CoveredOffsets);
                Assert.AreEqual(new Vector2Int(2, 2), snapshot.BoundingSize);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void TryRestoreSnapshot_WithCoveredOffsets_RestoresNonRectShape()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new FakeItemAdapter("tool"));
                var snapshot = new InventorySnapshot(
                    6,
                    new System.Collections.Generic.List<InventoryPlacementState>
                    {
                        new InventoryPlacementState(
                            0,
                            stack.Adapters,
                            PlacementOrientation.Rot0,
                            new Vector2Int(2, 2),
                            new[] { 0, 1, 3 },
                            new[]
                            {
                                new Vector2Int(0, 0),
                                new Vector2Int(1, 0),
                                new Vector2Int(0, 1)
                            })
                    });

                Assert.IsTrue(inventory.TryRestoreSnapshot(snapshot, logFailures: false));

                var restoredPlacement = inventory.GetPlacementAt(0);
                Assert.IsNotNull(restoredPlacement);
                CollectionAssert.AreEqual(new[] { 0, 1, 3 }, restoredPlacement.CoveredIndices);
                Assert.AreSame(restoredPlacement, inventory.GetPlacementAt(3));
                Assert.IsNull(inventory.GetPlacementAt(4));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));

                var coveredSlot = inventory.GetSlot(4);
                var entry = new DragEntry(stack.CreateCopy(), coveredSlot, inventory);

                Assert.AreSame(placement, entry.SourcePlacement);
                Assert.AreEqual(new Vector2Int(1, 1), entry.GrabOffset);
                Assert.AreEqual(new Vector2Int(2, 2), entry.BoundingSize);
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1));
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1));
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
        public void DropPreview_WhenShapeSizeLeavesGrid_ShowsInBoundsCells()
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
        public void DropPreview_WhenShapeSizeLeavesGridLeft_ShowsInBoundsCells()
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
        public void DropPreview_WhenShapeSizeLeavesGridUp_ShowsInBoundsCells()
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
        public void DropPreview_WithNonRectShape_UsesPlacementStoreOffsets()
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
                var shape = new TestPlacementShape(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1));
                var stack = ItemStackBuilder.Of(new ShapeAdapter("tool", shape));
                Assert.IsTrue(source.TryPlace(new PlacementRequest(stack, 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                Assert.IsTrue(target.TryGetDropPreviewSlots(
                    target.GetSlot(1),
                    context,
                    out var previewSlots,
                    out bool canPlace));

                Assert.IsTrue(canPlace);
                CollectionAssert.AreEqual(
                    new[] { 1, 2, 4 },
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1));
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
        public void DragContext_ShapedBatchAndStackedShapedEntries_AreDetected()
        {
            var shapedStack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));
            var stackedShapedStack = ItemStackBuilder.Of(
                new ShapeAdapter("crate", 2, 1),
                new ShapeAdapter("crate", 2, 1));

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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));
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
        public void StackableSlotInventory_ShapedItemsUseOnePlacementPerId()
        {
            // C2 (ShapedStacking-Plan.md): Stackable shaped is one-placement-per-id with a real stack.
            // A second identical shaped item merges into the existing placement (count grows) instead
            // of opening a second placement.
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .Build();

            try
            {
                Assert.IsTrue(inventory.TryAddStack(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1))));
                Assert.IsTrue(inventory.TryAddStack(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1))));

                Assert.AreEqual(2, inventory.GetSlot(0).Stack.Count);
                Assert.IsTrue(inventory.GetSlot(1).IsEmpty);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_StackableSameId_AutoMergesIntoExistingPlacement()
        {
            // C3/C4 (ShapedStacking-Plan.md): Stackable shaped is one-per-ID with a real stack.
            // Dropping a second same-id shaped item — even onto empty cells away from the existing
            // placement — auto-merges into that single placement (count grows), no second placement.
            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                // Drop onto cell 3 (away from the existing bag at {0,1}).
                var processor = new InventoryDropProcessor(target.GetSlot(3), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(1, summary.TransferredAmount);
                Assert.IsNull(source.GetPlacementAt(0), "Source placement consumed");

                var targetPlacement = target.GetPlacementAt(0);
                Assert.IsNotNull(targetPlacement);
                Assert.AreEqual(2, targetPlacement.Stack.Count, "Merged into the existing stack");
                CollectionAssert.AreEqual(new[] { 0, 1 }, targetPlacement.CoveredIndices);
                Assert.IsNull(target.GetPlacementAt(3), "one-per-ID: no second placement created");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_StackableFullStack_Rejects()
        {
            // one-per-ID + full existing stack: a second same-id shaped item cannot open a new placement.
            var strategy = new StackableItemStrategy();
            strategy.SetMaxStackSize(1, allowItemOverride: false);

            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(strategy).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(3), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsFalse(summary.Success, "Full one-per-ID stack must reject a second item");
                Assert.AreEqual(1, source.GetSlot(0).Stack.Count, "Source untouched");
                Assert.AreEqual(1, target.GetSlot(0).Stack.Count, "Target stack stays full at 1");
                Assert.IsNull(target.GetPlacementAt(3), "No second placement");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_SeparableOntoExisting_Merges()
        {
            // SeparableStacks: explicit drop ONTO an existing same-id placement merges into it.
            var source = new InventoryBuilder().WithStrategy(new SeparableStacksStrategy()).WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(new SeparableStacksStrategy()).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                // Drop ONTO the existing placement's anchor cell 0.
                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                var targetPlacement = target.GetPlacementAt(0);
                Assert.IsNotNull(targetPlacement);
                Assert.AreEqual(2, targetPlacement.Stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_SeparableAwayFromExisting_CreatesSecondPlacement()
        {
            // SeparableStacks: dropping a same-id shaped item onto EMPTY cells creates a second
            // separate placement (no auto-merge into the distant existing one).
            var source = new InventoryBuilder().WithStrategy(new SeparableStacksStrategy()).WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(new SeparableStacksStrategy()).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                // Drop onto empty cell 3 (away from {0,1}).
                var processor = new InventoryDropProcessor(target.GetSlot(3), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                var first = target.GetPlacementAt(0);
                var second = target.GetPlacementAt(3);
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                Assert.AreNotSame(first, second, "Separable: a second distinct placement is created");
                Assert.AreEqual(1, first.Stack.Count);
                Assert.AreEqual(1, second.Stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedStack_MoveWholeStack_ToEmptyGrid()
        {
            // C5 (ShapedStacking-Plan.md): a shaped placement with count > 1 can be dragged and moved
            // as a whole; the destination placement keeps the full count.
            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                Assert.AreEqual(3, entry.Stack.Count);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(3, summary.TransferredAmount);
                Assert.IsNull(source.GetPlacementAt(0), "Whole stack moved out of source");

                var targetPlacement = target.GetPlacementAt(0);
                Assert.IsNotNull(targetPlacement);
                Assert.AreEqual(3, targetPlacement.Stack.Count);
                CollectionAssert.AreEqual(new[] { 0, 1 }, targetPlacement.CoveredIndices);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedStack_SplitPartial_LeavesRemainderInSource()
        {
            // C5: dragging part of a shaped stack splits it — the dragged amount forms a new placement,
            // the remainder stays in the source placement (same footprint).
            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                // Simulate a split drag: grab only 2 of the 3.
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(2), dragSlot, source);
                Assert.AreEqual(2, entry.Stack.Count);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(3), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(2, summary.TransferredAmount);

                var sourcePlacement = source.GetPlacementAt(0);
                Assert.IsNotNull(sourcePlacement);
                Assert.AreEqual(1, sourcePlacement.Stack.Count, "Remainder stays in source");
                CollectionAssert.AreEqual(new[] { 0, 1 }, sourcePlacement.CoveredIndices);

                var targetPlacement = target.GetPlacementAt(3);
                Assert.IsNotNull(targetPlacement);
                Assert.AreEqual(2, targetPlacement.Stack.Count);
                CollectionAssert.AreEqual(new[] { 3, 4 }, targetPlacement.CoveredIndices);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedStackIntoUniqueGrid_PlacesOnlyOne()
        {
            // Unique caps a shaped placement at count 1, even when a count > 1 stack is dropped in.
            var source = new InventoryBuilder().WithStrategy(new StackableItemStrategy()).WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(new UniqueItemStrategy()).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                Assert.AreEqual(3, entry.Stack.Count);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(1, summary.TransferredAmount, "Unique grid accepts exactly one");
                Assert.AreEqual(1, target.GetPlacementAt(0).Stack.Count, "Unique never holds count > 1");
                Assert.AreEqual(2, source.GetPlacementAt(0).Stack.Count, "Remainder stays in source (partial)");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedStack_MergePartial_RespectsMaxStack()
        {
            // AllowPartial: merging a count > 1 stack into an existing shaped stack stops at maxStack;
            // the overflow is returned to the source.
            var strategy = new StackableItemStrategy();
            strategy.SetMaxStackSize(2, allowItemOverride: false);

            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(strategy).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(3), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(1, summary.TransferredAmount, "Only the maxStack room (1) merges");
                Assert.AreEqual(2, target.GetPlacementAt(0).Stack.Count, "Target filled to maxStack");
                Assert.AreEqual(2, source.GetPlacementAt(0).Stack.Count, "Overflow returned to source");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_StackableAutoMergeOff_DropAway_Rejects()
        {
            // C8: Stackable with auto-merge OFF does not auto-consolidate. Dropping a duplicate away from
            // the existing placement neither merges nor opens a second placement (strict one-per-ID) → reject.
            var strategy = new StackableItemStrategy();
            SetExplicitMergeOnly(strategy);

            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(strategy).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(3), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsFalse(summary.Success, "Auto-merge OFF + drop away → reject");
                Assert.IsNull(target.GetPlacementAt(3), "No second placement");
                Assert.AreEqual(1, target.GetPlacementAt(0).Stack.Count, "Existing stack untouched");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridToGrid_StackableAutoMergeOff_DropOntoExisting_Merges()
        {
            // C8: even with auto-merge OFF, an explicit drop ONTO the existing placement merges.
            var strategy = new StackableItemStrategy();
            SetExplicitMergeOnly(strategy);

            var source = new InventoryBuilder().WithFixedSlots(6).WithGridTopology(3, 2).Build();
            var target = new InventoryBuilder().WithStrategy(strategy).WithFixedSlots(6).WithGridTopology(3, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));

                var dragSlot = source.GetSlot(0);
                var entry = new DragEntry(dragSlot.Stack.CreateCopy(), dragSlot, source);
                var context = new DragContext(new[] { entry });

                // Drop ONTO the existing placement's anchor cell 0 (footprint overlap).
                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(2, target.GetPlacementAt(0).Stack.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedSlotToOccupiedSameId_Merges()
        {
            // On a slot inventory a shaped item occupies
            // one cell, so dropping it onto an occupied same-id slot merges through the normal pipeline.
            var source = new InventoryBuilder().WithFixedSlots(1).Build();
            var target = new InventoryBuilder().WithFixedSlots(1).Build();

            try
            {
                Assert.IsTrue(source.TryAddStack(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1))));
                Assert.IsTrue(target.TryAddStack(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1))));

                var sourceSlot = source.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.IsTrue(source.GetSlot(0).IsEmpty, "Source consumed by merge");
                Assert.AreEqual(2, target.GetSlot(0).Stack.Count, "Merged into the occupied same-id slot");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedSlotToOccupiedDifferentId_Swaps()
        {
            // Different non-mergeable items in slot inventories now swap through the universal
            // placement-based swap: each shaped item occupies one slot, so they exchange places.
            var source = new InventoryBuilder().WithFixedSlots(1).Build();
            var target = new InventoryBuilder().WithFixedSlots(1).Build();

            try
            {
                Assert.IsTrue(source.TryAddStack(ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1))));
                Assert.IsTrue(target.TryAddStack(ItemStackBuilder.Of(new ShapeAdapter("sword", 2, 1))));

                var sourceSlot = source.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context, DropRequestPolicy.WithSwap());

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual("sword", source.GetSlot(0).Stack.ID, "Source received the target item");
                Assert.AreEqual("bag", target.GetSlot(0).Stack.ID, "Target received the source item");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_ShapedGridSwap_DifferentFootprints_ExchangesPlacements()
        {
            // Universal swap on a grid: a 2x1 source item and a 1x1 target item exchange anchors,
            // each footprint recomputed by the grid topology.
            var source = new InventoryBuilder().WithFixedSlots(4).WithGridTopology(2, 2).Build();
            var target = new InventoryBuilder().WithFixedSlots(4).WithGridTopology(2, 2).Build();

            try
            {
                Assert.IsTrue(source.TryPlace(new PlacementRequest(
                    ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1)), 0)));
                Assert.IsTrue(target.TryPlace(new PlacementRequest(
                    ItemStackBuilder.Of(new FakeItemAdapter("gem")), 0)));

                var sourceSlot = source.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context, DropRequestPolicy.WithSwap());

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                // Source now holds the 1x1 gem at anchor 0 (single cell).
                Assert.AreEqual("gem", source.GetSlot(0).Stack.ID);
                Assert.IsNull(source.GetPlacementAt(1), "Gem must not extend past its single cell");

                // Target now holds the 2x1 blade at anchor 0 covering cells 0 and 1.
                var bladePlacement = target.GetPlacementAt(0);
                Assert.AreEqual("blade", bladePlacement.Stack.ID);
                Assert.AreEqual(2, bladePlacement.CoveredIndices.Count, "Blade footprint recomputed by target grid");
            }
            finally
            {
                InventoryBuilder.Destroy(source);
                InventoryBuilder.Destroy(target);
            }
        }

        [Test]
        public void ProcessDrop_CrossTopologySwap_GridToSlot_ExchangesItems()
        {
            // Universal swap across different topologies: a multi-cell item from a grid inventory
            // collapses to one slot in a slot inventory, and the slot item expands into the grid.
            var grid = new InventoryBuilder().WithFixedSlots(4).WithGridTopology(2, 2).Build();
            var slot = new InventoryBuilder().WithFixedSlots(1).Build();

            try
            {
                Assert.IsTrue(grid.TryPlace(new PlacementRequest(
                    ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1)), 0)));
                Assert.IsTrue(slot.TryAddStack(ItemStackBuilder.Of(new FakeItemAdapter("coin"))));

                // Drag the slot's coin onto the grid's bag anchor.
                var slotSourceSlot = slot.GetSlot(0);
                var entry = new DragEntry(slotSourceSlot.Stack.CreateCopy(), slotSourceSlot, slot);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(grid.GetSlot(0), grid, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context, DropRequestPolicy.WithSwap());

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);

                // Bag moved to the slot inventory, collapsed to one cell.
                Assert.AreEqual("bag", slot.GetSlot(0).Stack.ID);

                // Coin moved to the grid at anchor 0 as a single cell; cell 1 freed.
                Assert.AreEqual("coin", grid.GetSlot(0).Stack.ID);
                Assert.IsNull(grid.GetPlacementAt(1), "Coin occupies a single grid cell");
            }
            finally
            {
                InventoryBuilder.Destroy(grid);
                InventoryBuilder.Destroy(slot);
            }
        }

        [Test]
        public void ProcessDrop_ShapedStack_IntoEmptySlotInventory_KeepsCount()
        {
            // A shaped stack (count > 1) can be dropped into a slot inventory; it occupies one slot
            // and keeps its count.
            var source = new InventoryBuilder().WithFixedSlots(1).Build();
            var target = new InventoryBuilder().WithFixedSlots(2).Build();

            try
            {
                Assert.IsTrue(source.TryAddStack(ItemStackBuilder.Of(
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1))));
                Assert.AreEqual(3, source.GetSlot(0).Stack.Count);

                var sourceSlot = source.GetSlot(0);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, source);
                Assert.AreEqual(3, entry.Stack.Count);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(target.GetSlot(0), target, new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(context);

                Assert.IsTrue(summary.Success, summary.DropResult.FailureReason);
                Assert.AreEqual(3, summary.TransferredAmount);
                Assert.IsTrue(source.GetSlot(0).IsEmpty);
                Assert.AreEqual(3, target.GetSlot(0).Stack.Count);
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
                Assert.AreEqual(new Vector2Int(2, 2), summary.DropResult.BoundingSize);
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
                Assert.AreEqual(new Vector2Int(2, 2), removedContext.BoundingSize);
                CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, removedContext.CoveredIndices);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 3, 4 },
                    removedContext.CoveredBaseSlots.Select(slot => slot.Index).ToArray());

                Assert.IsNotNull(addedContext);
                Assert.AreEqual(4, addedContext.AnchorIndex);
                Assert.AreSame(target.GetSlot(4), addedContext.AnchorBaseSlot);
                Assert.AreEqual(PlacementOrientation.Rot0, addedContext.Orientation);
                Assert.AreEqual(new Vector2Int(2, 2), addedContext.BoundingSize);
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1));
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("blade", 2, 1));
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
        public void CanPlace_RejectsOverlapAndOutOfBoundsShapeSizes()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var shapedStack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(shapedStack, 0)));

                var overlappingStack = ItemStackBuilder.Of(new FakeItemAdapter("gem"));
                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(overlappingStack, 1)));

                var outOfBoundsStack = ItemStackBuilder.Of(new ShapeAdapter("shield", 2, 2));
                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(outOfBoundsStack, 2)));
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        [Test]
        public void CanPlace_AllowsMultiCellStack_GeometryIgnoresCount()
        {
            // C1 (ShapedStacking-Plan.md): placement geometry is independent of stack quantity.
            // A multi-cell footprint may carry a stack with count > 1; the count cap (max stack)
            // is enforced by the strategy / planner, not by placement geometry.
            var inventory = new InventoryBuilder()
                .WithFixedSlots(6)
                .Build();

            try
            {
                EnableGrid(inventory, 3, 2);
                var stack = ItemStackBuilder.Of(
                    new ShapeAdapter("bag", 2, 1),
                    new ShapeAdapter("bag", 2, 1));

                Assert.IsTrue(inventory.CanPlace(new PlacementRequest(stack, 0)));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var placement));
                Assert.AreEqual(2, placement.Stack.Count);
                CollectionAssert.AreEqual(new[] { 0, 1 }, placement.CoveredIndices);
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));

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
        public void SlotInventory_TreatsShapedItemAsSingleSlot()
        {
            var inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .Build();

            try
            {
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));

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
        public void TryPlace_CopiesInputStackOwnership()
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
                Assert.IsFalse(inventory.CanPlace(new PlacementRequest(replacement, 0)));

                var placement = inventory.GetPlacementAt(0);
                Assert.IsNotNull(placement);
                Assert.AreEqual(1, placement.Stack.Count);
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 2));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(stack, 0), out var originalPlacement));

                var snapshot = inventory.CaptureSnapshot();

                Assert.AreEqual(1, snapshot.Placements.Count);
                Assert.AreEqual(0, snapshot.Placements[0].AnchorIndex);
                Assert.AreEqual(PlacementOrientation.Rot0, snapshot.Placements[0].Orientation);
                Assert.AreEqual(new Vector2Int(2, 2), snapshot.Placements[0].BoundingSize);
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
                    Vector2Int.one,
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
                var stack = ItemStackBuilder.Of(new ShapeAdapter("bag", 2, 1));
                var snapshot = new InventorySnapshot(
                    1,
                    new System.Collections.Generic.List<InventoryPlacementState>
                    {
                        new InventoryPlacementState(
                            0,
                            stack.Adapters,
                            PlacementOrientation.Rot0,
                            new Vector2Int(2, 1),
                            new[] { 0, 1 })
                    });

                Assert.IsFalse(inventory.TryRestoreSnapshot(snapshot, logFailures: false));

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
                            Vector2Int.one,
                            new[] { 0 }),
                        new InventoryPlacementState(
                            0,
                            overlappingSnapshotStack.Adapters,
                            PlacementOrientation.Rot0,
                            Vector2Int.one,
                            new[] { 0 })
                    });

                Assert.IsFalse(inventory.TryRestoreSnapshot(snapshot, logFailures: false));

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
        public void ProcessDrop_SameInventorySwap_OverlappingResultFootprints_IsRejected()
        {
            // In one 4x1 grid: a 1x1 at cell 0 and a 3x1 anchored at cell 1 (covers 1,2,3).
            // Each side fits when both are vacated, but the swapped footprints overlap (the 3x1 moving
            // to anchor 0 would cover cell 1, where the 1x1 now sits), so the swap must be rejected
            // up front rather than failing at commit.
            var inventory = new InventoryBuilder()
                .WithFixedSlots(4)
                .WithGridTopology(4, 1)
                .Build();

            try
            {
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(
                    ItemStackBuilder.Of(new FakeItemAdapter("coin")), 0)));
                Assert.IsTrue(inventory.TryPlace(new PlacementRequest(
                    ItemStackBuilder.Of(new ShapeAdapter("rod", 3, 1)), 1)));

                var sourceSlot = inventory.GetSlot(0);
                var targetSlot = inventory.GetSlot(1);
                var entry = new DragEntry(sourceSlot.Stack.CreateCopy(), sourceSlot, inventory);
                var context = new DragContext(new[] { entry });

                var processor = new InventoryDropProcessor(
                    targetSlot,
                    inventory,
                    new GlobalRuleValidator());
                var summary = processor.ProcessDropWithSummary(
                    context,
                    DropRequestPolicy.WithSwap());

                Assert.IsFalse(
                    summary.Success,
                    "Overlapping same-inventory swap footprints must be rejected");
                Assert.AreEqual("coin", inventory.GetSlot(0).Stack.ID);
                Assert.AreEqual("rod", inventory.GetSlot(1).Stack.ID);
                Assert.AreEqual(3, inventory.GetPlacementAt(1).CoveredIndices.Count);
            }
            finally
            {
                InventoryBuilder.Destroy(inventory);
            }
        }

        private static IReadOnlyList<int> LegacyRequireAllCoveredCells(
            int anchorIndex,
            Vector2Int size,
            PlacementOrientation orientation,
            GridTopology topology,
            int slotCount)
        {
            topology = topology.Normalized();
            if (!topology.IsValidIndex(anchorIndex))
                return Array.Empty<int>();

            var anchorCell = topology.ToCell(anchorIndex);
            var orientedSize = GetOrientedSize(size, orientation);
            var result = new List<int>(orientedSize.x * orientedSize.y);

            for (int y = 0; y < orientedSize.y; y++)
            {
                for (int x = 0; x < orientedSize.x; x++)
                {
                    var cell = new Vector2Int(anchorCell.x + x, anchorCell.y + y);
                    if (!topology.Contains(cell))
                        return Array.Empty<int>();

                    int index = topology.ToIndex(cell);
                    if (index < 0 || index >= slotCount)
                        return Array.Empty<int>();

                    result.Add(index);
                }
            }

            return result;
        }

        private static IReadOnlyList<int> LegacyPreviewCoveredCells(
            Vector2Int anchorCell,
            Vector2Int size,
            PlacementOrientation orientation,
            GridTopology topology,
            int slotCount)
        {
            topology = topology.Normalized();
            var orientedSize = GetOrientedSize(size, orientation);
            var result = new List<int>(orientedSize.x * orientedSize.y);

            for (int y = 0; y < orientedSize.y; y++)
            {
                for (int x = 0; x < orientedSize.x; x++)
                {
                    var cell = new Vector2Int(anchorCell.x + x, anchorCell.y + y);
                    if (!topology.Contains(cell))
                        continue;

                    int index = topology.ToIndex(cell);
                    if (index >= 0 && index < slotCount)
                        result.Add(index);
                }
            }

            return result;
        }

        private static Vector2Int GetOrientedSize(Vector2Int size, PlacementOrientation orientation)
            => orientation == PlacementOrientation.Rot90 || orientation == PlacementOrientation.Rot270
                ? new Vector2Int(size.y, size.x)
                : size;

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

        // The auto-merge toggle is a private serialized field with no public accessor (config via inspector).
        // Tests force explicit-merge-only via reflection.
        private static void SetExplicitMergeOnly(StackableItemStrategy strategy)
        {
            typeof(StackableItemStrategy)
                .GetField("_explicitMergeOnly", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(strategy, true);
        }

        private sealed class ShapeAdapter : IItemAdapter, IItemPlacementShapeProvider
        {
            public ShapeAdapter(string itemId, int width, int height)
                : this(itemId, new RectPlacementShape(width, height))
            {
            }

            public ShapeAdapter(string itemId, IPlacementShape placementShape)
            {
                ItemId = itemId;
                DisplayName = itemId;
                PlacementShape = placementShape;
            }

            public string ItemId { get; }
            public string DisplayName { get; }
            public Sprite Icon => null;
            public IPlacementShape PlacementShape { get; }
        }

        private sealed class TestPlacementShape : IPlacementShape
        {
            private readonly IReadOnlyList<Vector2Int> _offsets;

            public TestPlacementShape(params Vector2Int[] offsets)
            {
                _offsets = Array.AsReadOnly(offsets ?? Array.Empty<Vector2Int>());
            }

            public IReadOnlyList<Vector2Int> GetOffsets(PlacementOrientation orientation) => _offsets;
            public bool SupportsOrientation(PlacementOrientation orientation) => true;
        }

        private sealed class SingleOrientationShape : IPlacementShape
        {
            private readonly IReadOnlyList<Vector2Int> _offsets;
            private readonly PlacementOrientation _supportedOrientation;

            public SingleOrientationShape(PlacementOrientation supportedOrientation, params Vector2Int[] offsets)
            {
                _supportedOrientation = supportedOrientation;
                _offsets = Array.AsReadOnly(offsets ?? Array.Empty<Vector2Int>());
            }

            public IReadOnlyList<Vector2Int> GetOffsets(PlacementOrientation orientation)
                => SupportsOrientation(orientation) ? _offsets : Array.Empty<Vector2Int>();

            public bool SupportsOrientation(PlacementOrientation orientation)
                => orientation == _supportedOrientation;
        }
    }
}
