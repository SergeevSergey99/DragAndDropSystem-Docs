using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UDND.Core;
using UDND.Inventories;

namespace UDND.Tests.PlayMode
{
    /// <summary>
    /// Regression coverage for slot rebuilds racing Unity's deferred Destroy.
    ///
    /// This belongs in PlayMode and nowhere else: Destroy() is only deferred to the end of the
    /// frame while the player loop runs. In EditMode the inventory takes the DestroyImmediate
    /// branch, the slot is gone before anything can observe it, and the whole class of bug these
    /// tests describe is unreproducible.
    /// </summary>
    [TestFixture]
    public class SlotRebuildPlayModeTests
    {
        private DragAndDropManager _manager;
        private UniversalInventory _source;
        private UniversalInventory _target;
        private UniversalInventory _rebuilt;

        [SetUp]
        public void SetUp()
        {
            _manager = DragAndDropManager.AutoCreateInstance;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            if (_manager != null && _manager.IsDragging)
                _manager.CancelDrag();

            InventoryBuilder.Destroy(_source);
            InventoryBuilder.Destroy(_target);
            InventoryBuilder.Destroy(_rebuilt);
            _source = null;
            _target = null;
            _rebuilt = null;

            if (DragAndDropManager.IsInstanceExist)
                UnityEngine.Object.DestroyImmediate(DragAndDropManager.Instance.gameObject);
            _manager = null;
        }

        /// <summary>
        /// The reported repro: a prefab ships with a design-time slot, ReInitSlots rebuilds the
        /// list, and initialization lands in the same frame — before the destroyed slot is
        /// actually gone. CacheSlots used to re-adopt it straight out of the container, leaving a
        /// "Missing" entry that survived every later null sweep.
        /// </summary>
        [UnityTest]
        public IEnumerator ReInitSlots_WhenInitializationLandsSameFrame_LeavesNoDestroyedSlots()
        {
            _rebuilt = BuildUninitializedInventory(initialSlots: 2, designTimeSlots: 1);

            var designTimeSlot = _rebuilt.Slots[0];
            Assert.IsTrue(designTimeSlot != null, "The design-time slot should exist before the rebuild");

            _rebuilt.ReInitSlots(3);

            // Same frame as the rebuild. Reading Strategy is one of the real entry points into
            // EnsureStrategyInitialized -> InitializeSlots -> CacheSlots; Start() and Initialize()
            // are the others. Whichever fires first must not re-scan the container and pick the
            // half-destroyed slot back up.
            Assert.IsNotNull(_rebuilt.Strategy);

            // Let the deferred Destroy actually land, turning any stale reference into a fake-null.
            yield return null;

            Assert.IsTrue(designTimeSlot == null, "The design-time slot should have been destroyed");
            Assert.AreEqual(3, _rebuilt.SlotCount, "ReInitSlots(3) must leave exactly three slots");

            for (int i = 0; i < _rebuilt.SlotCount; i++)
            {
                Assert.IsTrue(_rebuilt.GetSlot(i) != null, $"Slot {i} must not be a destroyed reference");
                Assert.AreEqual(i, _rebuilt.GetSlot(i).Index, $"Slot {i} must carry a dense index");
            }
        }

        /// <summary>
        /// The mechanism behind the fix above, pinned on its own. Destroy() is deferred, so the
        /// only thing that keeps a doomed slot out of the container — and therefore out of the
        /// next GetComponentsInChildren scan — is detaching it right away.
        /// </summary>
        [UnityTest]
        public IEnumerator ReInitSlots_RemovesDestroyedSlotsFromContainerImmediately()
        {
            _rebuilt = BuildUninitializedInventory(initialSlots: 2, designTimeSlots: 2);

            var container = _rebuilt.SlotContainer;
            Assert.AreEqual(2, container.childCount, "Test setup should start with two authored slots");

            _rebuilt.ReInitSlots(3);

            Assert.AreEqual(3, container.childCount,
                "Slots destroyed by the rebuild must leave the container in the same frame, " +
                "before Destroy() lands at the end of it");

            yield return null;

            Assert.AreEqual(3, container.childCount);
        }

        /// <summary>
        /// A destroyed slot left in the list must degrade to a skipped entry, not to a drag that
        /// never ends. This used to throw out of EndDrag before the state reset, so the item moved
        /// but the manager stayed in IsDragging forever — with an empty console, because the
        /// exception died inside the discarded CompleteDragAsync task.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyedSlotInSourceInventory_DoesNotStrandDrag()
        {
            BuildDragPair(sourceSlots: 3);

            // Destroyed behind the inventory's back, exactly as a mistimed rebuild used to leave it.
            UnityEngine.Object.DestroyImmediate(_source.GetSlot(2).gameObject);
            Assert.IsTrue(_source.GetSlot(2) == null, "Test setup should leave a stale slot entry");

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
            PushTarget();
            _manager.CompleteDrag();

            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging, "A stale slot entry must not strand the drag");
            Assert.IsFalse(_manager.HasActiveDropTarget);
            Assert.IsTrue(_source.GetSlot(0).IsEmpty);
            Assert.IsFalse(_target.GetSlot(0).IsEmpty);
        }

        /// <summary>
        /// The guarantee behind the fix, independent of what actually breaks: no matter what the
        /// end-of-drag visual refresh throws, the manager must come out of it droppable again.
        /// </summary>
        [UnityTest]
        public IEnumerator EndDrag_WhenVisualRefreshThrows_StillClearsDragState()
        {
            var throwing = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("ThrowingSource")
                .Build<ThrowingVisualsInventory>();
            _source = throwing;

            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("PlayModeTarget")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));

            Assert.IsTrue(_manager.StartDrag(_source.GetSlot(0)));
            PushTarget();

            // The refresh is reached from more than one place on the way down, and this test is
            // about the state machine surviving, not about how many times the failure is logged.
            LogAssert.ignoreFailingMessages = true;
            throwing.ThrowOnUpdateVisuals = true;

            _manager.CompleteDrag();
            yield return WaitForDragEnd();

            Assert.IsFalse(_manager.IsDragging, "A throwing visual refresh must not leave IsDragging set");
            Assert.IsFalse(_manager.HasActiveDropTarget);

            // And the manager is genuinely usable again, not just reporting a clean flag.
            throwing.ThrowOnUpdateVisuals = false;
            LogAssert.ignoreFailingMessages = false;

            _target.GetSlot(1).SetStack(ItemStackBuilder.Unique(1, "ruby"));
            Assert.IsTrue(_manager.StartDrag(_target.GetSlot(1)), "A later drag must still start");
            _manager.CancelDrag();
        }

        // ---------- helpers ----------

        /// <summary>
        /// A freshly instantiated prefab: authored slots already in the container and in the
        /// serialized list, but Start() has not run, so nothing is initialized yet.
        /// </summary>
        private static UniversalInventory BuildUninitializedInventory(int initialSlots, int designTimeSlots)
        {
            return new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(initialSlots)
                .WithPreexistingSlots(designTimeSlots)
                .WithoutLifecycleStart()
                .WithName("RebuiltInventory")
                .Build();
        }

        private void BuildDragPair(int sourceSlots)
        {
            _source = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(sourceSlots)
                .WithName("PlayModeSource")
                .Build();

            _target = new InventoryBuilder()
                .WithStrategy(new UniqueItemStrategy())
                .WithFixedSlots(2)
                .WithName("PlayModeTarget")
                .Build();

            _source.GetSlot(0).SetStack(ItemStackBuilder.Unique(1, "gem"));
        }

        private void PushTarget()
        {
            _manager.PushDropTarget(new FakeDropTarget(
                "target",
                _target.GetSlot(0),
                new InventoryDropProcessor(_target, _manager.GlobalRules)));
        }

        private IEnumerator WaitForDragEnd(int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && _manager.IsDragging; i++)
                yield return null;

            Assert.IsFalse(_manager.IsDragging, $"Drag did not end within {maxFrames} frames");
        }
    }

    /// <summary>
    /// Stands in for any inventory whose visual refresh can fail mid-teardown — a destroyed slot,
    /// a disposed presenter, a user callback. EndDrag has to survive all of them identically.
    /// </summary>
    public sealed class ThrowingVisualsInventory : UniversalInventory
    {
        public bool ThrowOnUpdateVisuals { get; set; }

        public override void UpdateAllVisuals()
        {
            if (ThrowOnUpdateVisuals)
                throw new InvalidOperationException("UpdateAllVisuals blew up");

            base.UpdateAllVisuals();
        }
    }
}
