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
            // The manager is a singleton that outlives a single test. If a test leaves it in
            // a broken state — CancelDrag can throw while cleaning up a destroyed target —
            // the teardown must still reach the DestroyImmediate below, otherwise the
            // poisoned instance leaks into every later test and one failure becomes dozens.
            TryCancelDrag();

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
        /// mid-drag will never pop itself, and the manager is left holding a reference to
        /// a view that no longer exists.
        ///
        /// FakeDropTargetBehaviour is a real MonoBehaviour so it can actually be destroyed
        /// while on the stack — the stack stores IDropTarget, so Unity's null-overload does
        /// not apply and EndDrag will call straight into the destroyed object.
        ///
        /// CancelDrag is used here rather than CompleteDrag: it runs EndDrag synchronously,
        /// so a failure surfaces as the actual exception instead of a fire-and-forget task
        /// that silently never completes.
        /// </summary>
        [Test]
        public void DestroyedDropTarget_OnCancelDrag_DoesNotThrow()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var stale = FakeDropTargetBehaviour.Create("StaleTarget");
            stale.PopSelfOnDisable = false; // the whole point: it never pops itself
            _manager.PushDropTarget(stale);
            Assert.AreSame(stale, _manager.ActiveDropTarget);

            UnityEngine.Object.DestroyImmediate(stale.gameObject);

            Assert.DoesNotThrow(() => _manager.CancelDrag(),
                "EndDrag must survive a drop target destroyed without popping itself");
            Assert.IsFalse(_manager.IsDragging);
            Assert.IsFalse(_manager.HasActiveDropTarget,
                "A drag that ended must leave no active target behind");
        }

        /// <summary>
        /// Same scenario on the async completion path, where a throw inside EndDrag's
        /// finally block would be swallowed by the fire-and-forget task and leave the
        /// manager permanently stuck in IsDragging.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyedDropTarget_OnCompleteDrag_StillEndsDrag()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var stale = FakeDropTargetBehaviour.Create("StaleTarget");
            stale.PopSelfOnDisable = false;
            _manager.PushDropTarget(stale);

            UnityEngine.Object.DestroyImmediate(stale.gameObject);

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsFalse(_manager.HasActiveDropTarget);
            Assert.IsFalse(_source.GetSlot(0).IsEmpty,
                "A destroyed target carries no processor, so nothing should transfer");
        }

        /// <summary>
        /// The realistic version of the stale-target case. A counter-only callback is pure
        /// managed code and stays silent on a destroyed object; a real presenter reads
        /// transform/Graphic (or, in UI Toolkit, a detached VisualElement) and raises
        /// MissingReferenceException from inside EndDrag's cleanup loop.
        ///
        /// EndDrag runs that loop in CompleteDragAsync's finally block, so an unguarded
        /// throw here does not just log — it strands the manager in IsDragging forever.
        /// </summary>
        [Test]
        public void DestroyedDropTarget_TouchingNativeState_DoesNotBreakCancelDrag()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var stale = FakeDropTargetBehaviour.Create("NativeTouchingTarget");
            stale.PopSelfOnDisable = false;
            stale.TouchNativeStateOnActivation = true;
            _manager.PushDropTarget(stale);
            Assert.AreSame(stale, _manager.ActiveDropTarget);

            UnityEngine.Object.DestroyImmediate(stale.gameObject);

            Assert.DoesNotThrow(() => _manager.CancelDrag(),
                "EndDrag must not propagate MissingReferenceException from a destroyed target");
            Assert.IsFalse(_manager.IsDragging);
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        /// <summary>
        /// Same target on the async completion path, where the throw would be swallowed by
        /// the fire-and-forget task instead of surfacing.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyedDropTarget_TouchingNativeState_StillEndsDrag()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var stale = FakeDropTargetBehaviour.Create("NativeTouchingTarget");
            stale.PopSelfOnDisable = false;
            stale.TouchNativeStateOnActivation = true;
            _manager.PushDropTarget(stale);

            UnityEngine.Object.DestroyImmediate(stale.gameObject);

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        /// <summary>
        /// The uGUI counterpart, component-disable flavor: this is the path DropAreaBase
        /// actually relies on, and the behavior a UITK adapter must reproduce via
        /// DetachFromPanelEvent.
        /// </summary>
        [Test]
        public void DropTarget_DisabledMidDrag_PopsItselfAndLeavesNoStaleEntry()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var target = FakeDropTargetBehaviour.Create("WellBehavedTarget");
            target.PopSelfOnDisable = true;
            _manager.PushDropTarget(target);
            Assert.AreSame(target, _manager.ActiveDropTarget);

            target.enabled = false;

            Assert.AreEqual(1, target.DisableCalls, "OnDisable must fire on component disable");
            Assert.IsTrue(target.PopWasAttempted);
            Assert.IsNull(_manager.ActiveDropTarget,
                "A target that pops itself on disable must not stay active");
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        /// <summary>
        /// The destruction flavor of the same contract. Split from the disable case because
        /// the two go through different Unity lifecycle paths: DisableCalls and
        /// PopWasAttempted tell us whether a failure here means "OnDisable never fired on
        /// GameObject destruction" or "the pop ran but did not clear the active target".
        ///
        /// Note the fake-null trap when this fails: a destroyed UnityEngine.Object prints as
        /// "null" but is not reference-null, so NUnit reports "Expected: null / But was:
        /// &lt;null&gt;". That message means the stale reference is still there.
        /// </summary>
        [Test]
        public void DropTarget_DestroyedMidDrag_PopsItselfAndLeavesNoStaleEntry()
        {
            BuildSourceAndTarget();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            var target = FakeDropTargetBehaviour.Create("WellBehavedTarget");
            target.PopSelfOnDisable = true;
            _manager.PushDropTarget(target);
            Assert.AreSame(target, _manager.ActiveDropTarget);

            int disableCallsBefore = target.DisableCalls;
            UnityEngine.Object.DestroyImmediate(target.gameObject);

            Assert.Greater(target.DisableCalls, disableCallsBefore,
                "OnDisable must fire when the target's GameObject is destroyed");
            Assert.IsTrue(target.PopWasAttempted,
                "OnDisable fired but never reached PopDropTarget");
            Assert.IsNull(_manager.ActiveDropTarget,
                "A destroyed target must not remain the active drop target");
            Assert.IsFalse(_manager.HasActiveDropTarget);
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

        /// <summary>
        /// Cancels an in-flight drag without letting a broken drag state abort teardown.
        /// Swallowing is correct only here: the test body has already asserted whatever it
        /// cared about, and the exception is a known consequence of destroying a live target.
        /// </summary>
        private void TryCancelDrag()
        {
            if (_manager == null || !_manager.IsDragging)
                return;

            try
            {
                _manager.CancelDrag();
            }
            catch (MissingReferenceException)
            {
                // A test deliberately destroyed a target that was still on the stack.
            }
        }

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
