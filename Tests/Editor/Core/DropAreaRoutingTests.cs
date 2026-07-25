using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UDND.Core;
using UDND.Inventories;
using UDND.UI;

namespace UDND.Tests.Core
{
    /// <summary>
    /// Routing tests for InventoryDropArea / DropAreaBase — area drop had no coverage at
    /// the UI boundary, only at the processor level.
    ///
    /// Area drop is in scope for the first UI Toolkit slice, and the uGUI implementation
    /// is the reference behavior a UITK area adapter has to reproduce: push on enter,
    /// pop on exit, pop on detach, and yield priority to a slot target stacked on top.
    /// </summary>
    [TestFixture]
    public class DropAreaRoutingTests
    {
        private GameObject _eventSystemObject;
        private GameObject _areaObject;
        private InventoryDropArea _area;
        private UniversalInventory _source;
        private UniversalInventory _target;
        private DragAndDropManager _manager;

        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("EventSystem");
            _eventSystemObject.AddComponent<EventSystem>();

            _manager = DragAndDropManager.AutoCreateInstance;

            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("AreaSource")
                .Build();
            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));
        }

        [TearDown]
        public void TearDown()
        {
            // Defensive: a drag left broken by another test must not stop this teardown from
            // destroying the singleton, or the damage spreads to every later test.
            TryCancelDrag();

            if (_areaObject != null)
                Object.DestroyImmediate(_areaObject);
            _areaObject = null;
            _area = null;

            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            _source = null;
            _target = null;

            if (DragAndDropManager.IsInstanceExist)
                Object.DestroyImmediate(DragAndDropManager.Instance.gameObject);
            _manager = null;

            if (_eventSystemObject != null)
                Object.DestroyImmediate(_eventSystemObject);
            _eventSystemObject = null;
        }

        // ---------- enter / exit ----------

        [Test]
        public void DropArea_OnPointerEnterDuringDrag_PushesItselfAsTarget()
        {
            BuildTargetWithArea(slots: 2);
            StartDrag();

            _area.OnPointerEnter(NewPointerEvent());

            Assert.AreSame(_area, _manager.ActiveDropTarget);
            Assert.IsTrue(_manager.HasActiveDropTarget);
            Assert.IsFalse(_manager.HasActiveSlotDropTarget,
                "An area target must not report a slot target");
        }

        [Test]
        public void DropArea_OnPointerEnter_WhenNotDragging_DoesNotPush()
        {
            BuildTargetWithArea(slots: 2);

            _area.OnPointerEnter(NewPointerEvent());

            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        [Test]
        public void DropArea_WhenInventoryCannotAccept_DoesNotPush()
        {
            // InventoryDropArea probes the drop processor before pushing. A full target
            // must never become the active target, otherwise the drag ends on a zone that
            // is guaranteed to reject it.
            BuildTargetWithArea(slots: 1);
            _target.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "occupied"));

            StartDrag();

            _area.OnPointerEnter(NewPointerEvent());

            Assert.IsNull(_manager.ActiveDropTarget,
                "A rejecting area must not be pushed onto the target stack");
        }

        [Test]
        public void DropArea_OnPointerExitDuringDrag_PopsItself()
        {
            BuildTargetWithArea(slots: 2);
            StartDrag();

            _area.OnPointerEnter(NewPointerEvent());
            Assert.AreSame(_area, _manager.ActiveDropTarget);

            _area.OnPointerExit(NewPointerEvent());

            Assert.IsNull(_manager.ActiveDropTarget);
            Assert.IsFalse(_manager.HasActiveDropTarget);
        }

        /// <summary>
        /// uGUI cleans up detached targets through OnDisable. This locks that in as the
        /// reference behavior — a UI Toolkit area has no OnDisable and will need an
        /// explicit DetachFromPanelEvent handler to match it.
        /// </summary>
        [Test]
        public void DropArea_DisabledDuringDrag_PopsItself()
        {
            BuildTargetWithArea(slots: 2);
            StartDrag();

            _area.OnPointerEnter(NewPointerEvent());
            Assert.AreSame(_area, _manager.ActiveDropTarget);

            _area.enabled = false;

            Assert.IsNull(_manager.ActiveDropTarget,
                "Disabling a drop area mid-drag must remove it from the target stack");
        }

        // ---------- nesting ----------

        [Test]
        public void SlotTargetOverArea_TakesPriority_AndRestoresAreaOnExit()
        {
            // The classic nesting: pointer is inside the area, then moves onto a slot
            // inside that same area, then leaves the slot. Getting this wrong is the
            // usual source of stale highlights.
            BuildTargetWithArea(slots: 2);
            StartDrag();

            _area.OnPointerEnter(NewPointerEvent());
            Assert.AreSame(_area, _manager.ActiveDropTarget);

            var slotTarget = new FakeDropTarget(
                "slot",
                _target.GetSlot(0),
                new InventoryDropProcessor(_target, _manager.GlobalRules));
            _manager.PushDropTarget(slotTarget);

            Assert.AreSame(slotTarget, _manager.ActiveDropTarget);
            Assert.IsTrue(_manager.HasActiveSlotDropTarget);

            _manager.PopDropTarget(slotTarget);

            Assert.AreSame(_area, _manager.ActiveDropTarget,
                "Leaving the slot must fall back to the area still under the pointer");
            Assert.IsFalse(_manager.HasActiveSlotDropTarget);
        }

        // ---------- completion ----------

        [UnityTest]
        public IEnumerator AreaDrop_CompletedThroughManager_TransfersItem()
        {
            BuildTargetWithArea(slots: 2);
            StartDrag();

            _area.OnPointerEnter(NewPointerEvent());
            Assert.AreSame(_area, _manager.ActiveDropTarget);

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
            Assert.IsFalse(_target.GetSlot(0).IsEmpty);
            Assert.IsNull(_manager.ActiveDropTarget);
        }

        /// <summary>
        /// Dynamic slots must be created by the transfer pipeline during execution, not by
        /// hover or preview. A view that creates slots on hover would mutate the inventory
        /// from the presentation layer, which the architecture forbids.
        /// </summary>
        [UnityTest]
        public IEnumerator AreaDrop_WithDynamicSlots_CreatesSlotOnlyDuringExecution()
        {
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(0)
                .WithSlotManagementSettings(new DynamicSlotManagementSettings())
                .WithName("DynamicAreaTarget")
                .Build();
            AttachArea(_target);

            int slotCountBeforeHover = _target.SlotCount;

            StartDrag();
            _area.OnPointerEnter(NewPointerEvent());

            Assert.AreSame(_area, _manager.ActiveDropTarget,
                "A dynamic-slot inventory must accept the area drop while still empty");
            Assert.AreEqual(slotCountBeforeHover, _target.SlotCount,
                "Hovering an area must not create slots");

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.Greater(_target.SlotCount, slotCountBeforeHover,
                "The transfer pipeline must create the slot during execution");
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
        }

        // ---------- helpers ----------

        private void BuildTargetWithArea(int slots)
        {
            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(slots)
                .WithName("AreaTarget")
                .Build();
            AttachArea(_target);
        }

        private void AttachArea(UniversalInventory inventory)
        {
            _areaObject = new GameObject("InventoryDropArea");
            _areaObject.transform.SetParent(inventory.transform);
            _area = _areaObject.AddComponent<InventoryDropArea>();

            typeof(InventoryDropArea)
                .GetField("_inventory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_area, inventory);
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
                // A destroyed target was still on the stack; teardown must continue anyway.
            }
        }

        private void StartDrag()
        {
            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
        }

        private static PointerEventData NewPointerEvent()
            => new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };

        private IEnumerator WaitForDragEnd(int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && _manager.IsDragging; i++)
                yield return null;

            Assert.IsFalse(_manager.IsDragging, $"Drag did not end within {maxFrames} frames");
        }
    }
}
