using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UDND.Core;
using UDND.Inventories;

namespace UDND.Tests.Core
{
    /// <summary>
    /// Lifecycle tests for DragAndDropManager: CompleteDrag, CancelDrag and EndDrag.
    ///
    /// These cover the manager-level seam a second UI backend (UI Toolkit) has to drive,
    /// which the strategy/processor suites do not touch — they call InventoryDropProcessor
    /// directly and never go through the manager.
    ///
    /// Note: CompleteDrag is fire-and-forget (`_ = CompleteDragAsync(...)`), so any test
    /// that asserts post-drop state must yield until the manager settles. WaitForDragEnd
    /// encapsulates that.
    /// </summary>
    [TestFixture]
    public class DragLifecycleTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;
        private DragAndDropManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = DragAndDropManager.AutoCreateInstance;
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null && _manager.IsDragging)
                _manager.CancelDrag();

            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;

            if (DragAndDropManager.IsInstanceExist)
                UnityEngine.Object.DestroyImmediate(DragAndDropManager.Instance.gameObject);
            _manager = null;
        }

        // ---------- CompleteDrag ----------

        [UnityTest]
        public IEnumerator CompleteDrag_WithoutActiveTarget_EndsDragWithoutMutation()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
            Assert.IsNull(_manager.ActiveDropTarget,
                "TestSlot has no IDropTarget component, so no target should be active");

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsNull(_manager.CurrentContext);
            Assert.IsFalse(_source.GetSlot(0).IsEmpty, "No processor means no mutation");
            Assert.AreEqual(0, CountFilledSlots(_target));
        }

        [UnityTest]
        public IEnumerator CompleteDrag_WithInventoryTarget_MovesItemAndClearsDragState()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var target = new FakeDropTarget(
                "targetSlot0",
                _target.GetSlot(0),
                new InventoryDropProcessor(_target, _manager.GlobalRules));
            _manager.PushDropTarget(target);

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsNull(_manager.CurrentContext);
            Assert.IsNull(_manager.ActiveDropTarget);
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
            Assert.AreEqual(1, CountFilledSlots(_target));
        }

        [UnityTest]
        public IEnumerator CompleteDrag_CalledTwice_SecondCallIsIgnored()
        {
            // A UI Toolkit adapter can plausibly receive both PointerUpEvent and
            // PointerCaptureOutEvent for the same release. The _isCompletingDrag guard
            // must make the second completion a no-op rather than a double transfer.
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var processor = new FakeDropProcessor();
            _manager.PushDropTarget(new FakeDropTarget("target", null, processor));

            _manager.CompleteDrag();
            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.AreEqual(1, processor.ProcessCalls,
                "Second CompleteDrag must not run the drop a second time");
            Assert.IsFalse(_manager.IsDragging);
        }

        [UnityTest]
        public IEnumerator CompleteDrag_WithAsyncDomainHandler_DoesNotMutateBeforeVeto()
        {
            // The async veto is already covered at the processor level. This asserts the
            // same invariant one layer up: routing a drop through the manager must not
            // bypass IAsyncTransferDomainHandler, which is the failure mode a synchronous
            // UITK drop callback would introduce.
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            var binding = _target.gameObject.AddComponent<UDND.Tests.Inventories.TestAsyncTransferDomainBinding>();
            binding.RejectTransferStart = true;
            _target.Initialize(binding);

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
            _manager.PushDropTarget(new FakeDropTarget(
                "target",
                _target.GetSlot(0),
                new InventoryDropProcessor(_target, _manager.GlobalRules)));

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.AreEqual(1, binding.StartCalls, "Async domain handler must be consulted once");
            Assert.IsFalse(_source.GetSlot(0).IsEmpty, "Veto must leave the source untouched");
            Assert.AreEqual(0, CountFilledSlots(_target));
            Assert.IsFalse(_manager.IsDragging);
        }

        // ---------- EndDrag / target cleanup ----------

        [UnityTest]
        public IEnumerator EndDrag_DeactivatesEveryTargetInStack()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var area = new FakeDropTarget("area");
            var slot = new FakeDropTarget("slot");
            _manager.PushDropTarget(area);
            _manager.PushDropTarget(slot);

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(area.IsActive, "Stacked-but-inactive target must be deactivated on EndDrag");
            Assert.IsFalse(slot.IsActive, "Active target must be deactivated on EndDrag");
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        [Test]
        public void CancelDrag_ClearsContextAndDeactivatesTargets()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var target = new FakeDropTarget("target");
            _manager.PushDropTarget(target);
            Assert.IsTrue(target.IsActive);

            _manager.CancelDrag();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsNull(_manager.CurrentContext);
            Assert.IsFalse(target.IsActive);
            Assert.IsFalse(_source.GetSlot(0).IsDraggedFromVisualState,
                "Cancel must clear the dragged-from visual state on the source slot");
        }

        [Test]
        public void CancelDrag_WhenNotDragging_IsIgnored()
        {
            Assert.IsFalse(_manager.IsDragging);
            Assert.DoesNotThrow(() => _manager.CancelDrag());
        }

        /// <summary>
        /// uGUI relies on DropAreaBase.OnDisable -> PopDropTarget to clean up. A
        /// VisualElement has no OnDisable, so a UI Toolkit target detached from its panel
        /// mid-drag will never pop itself. This asserts the manager survives a target that
        /// disappears without popping, which is the "detached element must not stay a
        /// target" invariant.
        /// </summary>
        [UnityTest]
        public IEnumerator DisappearingDropTarget_MidDrag_DoesNotBreakCompletion()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var stale = new FakeDropTarget("stale");
            _manager.PushDropTarget(stale);

            // Simulate the view going away without notifying the manager.
            var staleSlotHost = new GameObject("StaleTargetHost");
            UnityEngine.Object.DestroyImmediate(staleSlotHost);

            Assert.DoesNotThrow(() => _manager.CompleteDrag());
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsFalse(_manager.HasActiveDropTarget,
                "A drag that ended must leave no active target behind");
            Assert.IsFalse(stale.IsActive);
        }

        // ---------- RotateCurrentDrag ----------

        [Test]
        public void RotateCurrentDrag_WhenNotDragging_ReturnsFalse()
        {
            Assert.IsFalse(_manager.RotateCurrentDrag());
        }

        [Test]
        public void RotateCurrentDrag_UsesTopologyOrientationCount_NotFourNinetyDegreeSteps()
        {
            // Guards the design rule that orientation is topology-defined. A backend must
            // not assume 4 steps of 90 degrees: rotating by OrientationCount is a full turn
            // and must be a no-op regardless of how many steps that is.
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(6)
                .WithGridTopology(3, 2)
                .WithName("RotationSource")
                .Build();

            Assert.IsTrue(_source.TryPlace(new PlacementRequest(
                ItemStackBuilder.Of(new ShapedTestAdapter("blade", 2, 1)),
                0)));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var topology = ((IPlacementInventory)_source).Topology;
            Assert.IsNotNull(topology);
            Assert.Greater(topology.OrientationCount, 1);

            var initialOrientation = _manager.CurrentContext.Entries[0].Orientation;

            Assert.IsTrue(_manager.RotateCurrentDrag(topology.OrientationCount),
                "A full turn must be accepted");
            Assert.AreEqual(
                initialOrientation,
                _manager.CurrentContext.Entries[0].Orientation,
                "Rotating by OrientationCount must return the entry to its original orientation");

            Assert.IsTrue(_manager.RotateCurrentDrag(1));
            Assert.AreNotEqual(
                initialOrientation,
                _manager.CurrentContext.Entries[0].Orientation,
                "A single step must actually change orientation");
        }

        // ---------- helpers ----------

        private void BuildSourceAndTarget()
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("LifecycleSource")
                .Build();

            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("LifecycleTarget")
                .Build();
        }

        /// <summary>
        /// CompleteDrag returns immediately and finishes on the async continuation.
        /// Yield until the manager reports the drag is over, with a frame budget so a
        /// regression surfaces as a readable failure instead of an editor hang.
        /// </summary>
        private IEnumerator WaitForDragEnd(int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && _manager.IsDragging; i++)
                yield return null;

            Assert.IsFalse(_manager.IsDragging,
                $"Drag did not end within {maxFrames} frames");
        }

        private static int CountFilledSlots(IInventory inventory)
        {
            int count = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    count++;
            }
            return count;
        }

        /// <summary>Multi-cell adapter so rotation has something to rotate.</summary>
        private sealed class ShapedTestAdapter : IItemAdapter, IItemPlacementShapeProvider
        {
            public ShapedTestAdapter(string itemId, int width, int height)
            {
                ItemId = itemId;
                DisplayName = itemId;
                PlacementShape = new RectPlacementShape(width, height);
            }

            public string ItemId { get; }
            public string DisplayName { get; }
            public Sprite Icon => null;
            public IPlacementShape PlacementShape { get; }
        }
    }
}
