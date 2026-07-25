using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;

namespace UDND.Tests.Core
{
    /// <summary>
    /// Tests for DragAndDropManager's drop-target stack (PushDropTarget / PopDropTarget /
    /// ActivateTopTarget).
    ///
    /// This is the manual seam a non-uGUI backend drives directly: it has no Selectable,
    /// no PointerEventData and no GetComponent&lt;IDropTarget&gt; lookup, so push/pop is the
    /// only way it can register a hover target. None of it was covered before.
    ///
    /// IDropTarget carries no Unity types, so FakeDropTarget keeps these tests free of
    /// GameObjects and an EventSystem.
    /// </summary>
    [TestFixture]
    public class DropTargetStackTests
    {
        private UniversalInventory _inventory;
        private DragAndDropManager _manager;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _manager = DragAndDropManager.AutoCreateInstance;
            _log = new List<string>();

            _inventory = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("StackTestInventory")
                .Build();
            _inventory.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));
        }

        [TearDown]
        public void TearDown()
        {
            // Defensive: a drag left broken by another test must not stop this teardown from
            // destroying the singleton, or the damage spreads to every later test.
            TryCancelDrag();

            InventoryBuilder.Destroy(_inventory);
            _inventory = null;

            if (DragAndDropManager.IsInstanceExist)
                Object.DestroyImmediate(DragAndDropManager.Instance.gameObject);
            _manager = null;
        }

        // ---------- push ----------

        /// <summary>
        /// PushDropTarget returns early when no drag is active. A UI Toolkit adapter that
        /// pushes on PointerEnterEvent before the drag has started will be silently
        /// ignored — pinning the behavior here so it is a documented contract rather than
        /// something discovered during integration.
        /// </summary>
        [Test]
        public void PushDropTarget_WhenNotDragging_IsIgnored()
        {
            var target = new FakeDropTarget("target", activationLog: _log);

            _manager.PushDropTarget(target);

            Assert.IsFalse(_manager.HasActiveDropTarget);
            Assert.AreEqual(0, target.ActivateCount);
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void PushDropTarget_Null_IsIgnored()
        {
            StartDrag();
            Assert.DoesNotThrow(() => _manager.PushDropTarget(null));
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        [Test]
        public void PushDropTarget_ActivatesTarget()
        {
            StartDrag();
            var target = new FakeDropTarget("target", activationLog: _log);

            _manager.PushDropTarget(target);

            Assert.IsTrue(target.IsActive);
            Assert.AreEqual(1, target.ActivateCount);
            Assert.AreSame(target, _manager.ActiveDropTarget);
            Assert.IsTrue(_manager.HasActiveDropTarget);
        }

        [Test]
        public void PushDropTarget_Duplicate_DoesNotGrowStackOrReactivate()
        {
            StartDrag();
            var target = new FakeDropTarget("target", activationLog: _log);

            _manager.PushDropTarget(target);
            _manager.PushDropTarget(target);

            Assert.AreEqual(1, target.ActivateCount, "Duplicate push must be ignored");
            Assert.AreEqual(0, target.DeactivateCount);

            // If the duplicate had been stacked, one pop would leave a stale copy behind.
            _manager.PopDropTarget(target);
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        [Test]
        public void PushDropTarget_SecondTarget_DeactivatesPreviousTop()
        {
            StartDrag();
            var area = new FakeDropTarget("area", activationLog: _log);
            var slot = new FakeDropTarget("slot", activationLog: _log);

            _manager.PushDropTarget(area);
            _manager.PushDropTarget(slot);

            Assert.IsFalse(area.IsActive, "Previous top must be deactivated");
            Assert.IsTrue(slot.IsActive);
            Assert.AreSame(slot, _manager.ActiveDropTarget);
            CollectionAssert.AreEqual(
                new[] { "area:active", "area:inactive", "slot:active" },
                _log);
        }

        // ---------- pop ----------

        [Test]
        public void PopDropTarget_TopTarget_ReactivatesPrevious()
        {
            // The nested case that produces stale highlights when it goes wrong:
            // a slot target sitting on top of an area target, then the pointer leaves
            // the slot but stays inside the area.
            StartDrag();
            var area = new FakeDropTarget("area", activationLog: _log);
            var slot = new FakeDropTarget("slot", activationLog: _log);

            _manager.PushDropTarget(area);
            _manager.PushDropTarget(slot);
            _log.Clear();

            _manager.PopDropTarget(slot);

            Assert.IsFalse(slot.IsActive);
            Assert.IsTrue(area.IsActive, "Popping the top must reactivate the target below it");
            Assert.AreSame(area, _manager.ActiveDropTarget);
            CollectionAssert.AreEqual(new[] { "slot:inactive", "area:active" }, _log);
        }

        [Test]
        public void PopDropTarget_NonTopTarget_DoesNotDisturbActiveTarget()
        {
            StartDrag();
            var area = new FakeDropTarget("area", activationLog: _log);
            var slot = new FakeDropTarget("slot", activationLog: _log);

            _manager.PushDropTarget(area);
            _manager.PushDropTarget(slot);
            _log.Clear();

            _manager.PopDropTarget(area);

            Assert.IsTrue(slot.IsActive, "Removing a buried target must not touch the active one");
            Assert.AreSame(slot, _manager.ActiveDropTarget);
            CollectionAssert.IsEmpty(_log, "Removing a buried target raises no activation changes");
        }

        [Test]
        public void PopDropTarget_UnknownTarget_IsIgnored()
        {
            StartDrag();
            var pushed = new FakeDropTarget("pushed", activationLog: _log);
            var never = new FakeDropTarget("never", activationLog: _log);

            _manager.PushDropTarget(pushed);
            _log.Clear();

            _manager.PopDropTarget(never);

            Assert.IsTrue(pushed.IsActive);
            Assert.AreSame(pushed, _manager.ActiveDropTarget);
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void PopDropTarget_Null_IsIgnored()
        {
            StartDrag();
            Assert.DoesNotThrow(() => _manager.PopDropTarget(null));
        }

        [Test]
        public void PopDropTarget_LastTarget_ClearsActiveTargetAndContextTarget()
        {
            StartDrag();
            var target = new FakeDropTarget(
                "target",
                _inventory.GetSlot(1),
                new InventoryDropProcessor(_inventory, _manager.GlobalRules),
                _log);

            _manager.PushDropTarget(target);
            Assert.IsTrue(_manager.HasActiveSlotDropTarget);

            // Push itself does not set the context target — the preview/drop path does.
            // Seed it the way a hover preview would, so the clearing behavior is observable.
            _manager.CurrentContext.SetTarget(_inventory.GetSlot(1), _inventory);

            _manager.PopDropTarget(target);

            Assert.IsNull(_manager.ActiveDropTarget);
            Assert.IsFalse(_manager.HasActiveDropTarget);
            Assert.IsFalse(_manager.HasActiveSlotDropTarget);
            Assert.IsNull(_manager.CurrentContext.TargetInventory,
                "Emptying the stack must clear the drag context target");
            Assert.IsNull(_manager.CurrentContext.TargetBaseSlot);
        }

        // ---------- events ----------

        [Test]
        public void TargetTransition_RaisesDragExitThenDragEnter()
        {
            // UITK presenters will subscribe to these, so the ordering across a target
            // switch is part of the contract, not an implementation detail.
            StartDrag();
            var first = new FakeDropTarget("first");
            var second = new FakeDropTarget("second");

            var events = new List<string>();
            System.Action<DragContext> onEnter = _ => events.Add("enter");
            System.Action<DragContext> onExit = _ => events.Add("exit");

            UDNDEvents.OnDragEnterSlot += onEnter;
            UDNDEvents.OnDragExitSlot += onExit;
            try
            {
                _manager.PushDropTarget(first);
                events.Clear();

                _manager.PushDropTarget(second);

                CollectionAssert.AreEqual(new[] { "exit", "enter" }, events);
            }
            finally
            {
                UDNDEvents.OnDragEnterSlot -= onEnter;
                UDNDEvents.OnDragExitSlot -= onExit;
            }
        }

        // ---------- helpers ----------

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
                // A destroyed target was still on the stack; teardown must continue anyway.
            }
        }

        private void StartDrag()
        {
            Assert.IsTrue(_manager.StartDrag(_inventory.GetSlot(0)),
                "Drag must start for drop-target tests to be meaningful");

            // TestSlot has no IDropTarget component, so StartDrag's implicit
            // ActivateDropTargetForSlot finds nothing and the stack begins empty.
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }
    }
}
