using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UDND.Core;
using UDND.Inventories;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Tests.PlayMode
{
    /// <summary>
    /// PlayMode baseline for the drag lifecycle.
    ///
    /// This assembly exists because EditMode cannot cover everything the UI backends need:
    /// EditMode does not run Awake/OnEnable/OnDisable for ordinary MonoBehaviours, and a
    /// UI Toolkit panel with real PointerEventBase delivery cannot be driven there at all.
    /// The EditMode suite works around the first limitation with [ExecuteAlways] on its
    /// fakes; that workaround is a testing artifact, and these tests exist to prove the
    /// production lifecycle behaves correctly without it.
    ///
    /// Keep this assembly small. It is for behavior that genuinely requires a running
    /// player loop — everything else belongs in the (much faster) EditMode suite.
    /// </summary>
    [TestFixture]
    public class DragLifecyclePlayModeTests
    {
        private UniversalInventory _source;
        private UniversalInventory _target;
        private DragAndDropManager _manager;
        private GameObject _targetHost;

        [SetUp]
        public void SetUp()
        {
            _manager = DragAndDropManager.AutoCreateInstance;

            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("PlayModeSource")
                .Build();

            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("PlayModeTarget")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));
        }

        [TearDown]
        public void TearDown()
        {
            TryCancelDrag();

            if (_targetHost != null)
                Object.DestroyImmediate(_targetHost);
            _targetHost = null;

            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;

            if (DragAndDropManager.IsInstanceExist)
                Object.DestroyImmediate(DragAndDropManager.Instance.gameObject);
            _manager = null;
        }

        /// <summary>
        /// Harness check plus a real assertion: CompleteDrag is fire-and-forget, so this is
        /// the first place the async completion path runs against an actual player loop
        /// rather than manually pumped EditMode frames.
        /// </summary>
        [UnityTest]
        public IEnumerator DragToDrop_CompletesUnderRealFrames()
        {
            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));

            _manager.PushDropTarget(new FakeDropTarget(
                "target",
                _target.GetSlot(0),
                new InventoryDropProcessor(_target, _manager.GlobalRules)));

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
            Assert.IsFalse(_target.GetSlot(0).IsEmpty);
            Assert.IsNull(_manager.ActiveDropTarget);
        }

        /// <summary>
        /// The lifecycle contract without the EditMode crutch. A plain MonoBehaviour — no
        /// [ExecuteAlways], no Selectable base class — must receive OnDisable and pop itself
        /// off the target stack. This is the behavior a UI Toolkit adapter has to reproduce
        /// through DetachFromPanelEvent, since VisualElement has no OnDisable equivalent.
        /// </summary>
        [UnityTest]
        public IEnumerator PlainMonoBehaviourTarget_DisabledMidDrag_PopsItself()
        {
            var target = PlainDropTarget.Create(_target.GetSlot(0), _target, _manager.GlobalRules);
            _targetHost = target.gameObject;

            // Let Unity run the normal enable path before the drag starts.
            yield return null;

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
            _manager.PushDropTarget(target);
            Assert.AreSame(target, _manager.ActiveDropTarget);

            target.enabled = false;
            yield return null;

            Assert.AreEqual(1, target.DisableCalls,
                "PlayMode must deliver OnDisable to a plain MonoBehaviour without [ExecuteAlways]");
            Assert.IsNull(_manager.ActiveDropTarget,
                "A disabled target must not remain active");
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        /// <summary>
        /// Destruction flavor of the same contract, and the scenario the EndDrag guard was
        /// added for: a target whose GameObject disappears mid-drag must not strand the
        /// manager in IsDragging.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyedTarget_MidDrag_StillEndsDrag()
        {
            var target = PlainDropTarget.Create(_target.GetSlot(0), _target, _manager.GlobalRules);
            target.PopSelfOnDisable = false;
            yield return null;

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
            _manager.PushDropTarget(target);

            Object.DestroyImmediate(target.gameObject);

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging);
            Assert.IsFalse(_manager.HasActiveDropTarget);
            Assert.IsFalse(_source.GetSlot(0).IsEmpty,
                "A destroyed target carries no usable processor, so nothing should transfer");
        }

        // ---------- helpers ----------

        private IEnumerator WaitForDragEnd(int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && _manager.IsDragging; i++)
                yield return null;

            Assert.IsFalse(_manager.IsDragging, $"Drag did not end within {maxFrames} frames");
        }

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
    }

    /// <summary>
    /// Deliberately a plain MonoBehaviour: no [ExecuteAlways] and no Selectable base, so it
    /// only receives lifecycle callbacks under a real player loop. That is the point — it
    /// verifies production lifecycle assumptions the EditMode fakes have to simulate.
    /// </summary>
    public sealed class PlainDropTarget : MonoBehaviour, IDropTarget
    {
        private BaseSlot _targetSlot;
        private IDropProcessor _processor;

        public int DisableCalls { get; private set; }
        public bool PopSelfOnDisable { get; set; } = true;

        public static PlainDropTarget Create(
            BaseSlot targetSlot,
            IInventory targetInventory,
            GlobalRuleValidator globalRules)
        {
            var go = new GameObject("PlainDropTarget");
            var target = go.AddComponent<PlainDropTarget>();
            target._targetSlot = targetSlot;
            target._processor = new InventoryDropProcessor(targetInventory, globalRules);
            return target;
        }

        public BaseSlot GetTargetSlot() => _targetSlot;

        public IDropProcessor GetDropProcessor() => _processor;

        public void OnBecomeActiveTarget() { }

        public void OnBecomeInactiveTarget() { }

        private void OnDisable()
        {
            DisableCalls++;

            if (!PopSelfOnDisable || !DragAndDropManager.IsInstanceExist)
                return;

            var manager = DragAndDropManager.Instance;
            if (manager != null && manager.IsDragging)
                manager.PopDropTarget(this);
        }
    }
}
