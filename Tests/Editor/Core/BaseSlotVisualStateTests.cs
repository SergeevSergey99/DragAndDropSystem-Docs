using NUnit.Framework;
using UnityEngine;
using UDND.Slots;
using UDND.Tests;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class BaseSlotVisualStateTests
    {
        private GameObject _slotObject;
        private VisualStateTestSlot _slot;

        [SetUp]
        public void SetUp()
        {
            _slotObject = new GameObject("VisualStateSlot");
            _slot = _slotObject.AddComponent<VisualStateTestSlot>();
            _slot.SetStack(ItemStackBuilder.Unique(1, "gem"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_slotObject != null)
                Object.DestroyImmediate(_slotObject);
        }

        [Test]
        public void SetDraggedFrom_UsesSourceDragRendering()
        {
            _slot.SetDraggedFrom(true);

            Assert.AreEqual(VisualStateTestSlot.RenderState.DraggedFrom, _slot.LastRenderState);
        }

        [Test]
        public void SetDraggedTo_UsesTargetDragRendering()
        {
            _slot.SetDraggedTo(true);

            Assert.AreEqual(VisualStateTestSlot.RenderState.DraggedTo, _slot.LastRenderState);
        }

        [Test]
        public void UpdateVisuals_WhenBothDragStatesAreSet_SourceRenderingTakesPrecedence()
        {
            _slot.SetDraggedTo(true);
            _slot.SetDraggedFrom(true);

            Assert.AreEqual(VisualStateTestSlot.RenderState.DraggedFrom, _slot.LastRenderState);
        }
    }

    public sealed class VisualStateTestSlot : TestSlot
    {
        public enum RenderState
        {
            Empty,
            Filled,
            DraggedFrom,
            DraggedTo
        }

        public RenderState LastRenderState { get; private set; }

        protected override void RenderEmpty() => LastRenderState = RenderState.Empty;
        protected override void RenderFilled() => LastRenderState = RenderState.Filled;
        protected override void RenderFilledAndDraggedFrom() => LastRenderState = RenderState.DraggedFrom;
        protected override void RenderFilledAndDraggedTo() => LastRenderState = RenderState.DraggedTo;
    }
}
