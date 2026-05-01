using NUnit.Framework;
using UnityEngine;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Tests.Core
{
    [TestFixture]
    public class UniversalSlotHighlightTests
    {
        private GameObject _slotObject;
        private UniversalSlot _slot;

        [SetUp]
        public void SetUp()
        {
            _slotObject = new GameObject("UniversalSlot", typeof(RectTransform), typeof(UniversalSlot));
            _slot = _slotObject.GetComponent<UniversalSlot>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_slotObject != null)
                Object.DestroyImmediate(_slotObject);
        }

        [Test]
        public void Highlight_WithoutExplicitGraphic_CreatesSlotLevelOverlay()
        {
            _slot.Highlight(true);

            var highlight = _slotObject.transform.Find("Slot Drop Preview Highlight");
            Assert.IsNotNull(highlight);
            Assert.IsTrue(highlight.gameObject.activeSelf);

            var image = highlight.GetComponent("Image");
            Assert.IsNotNull(image);
            var raycastTarget = (bool)image.GetType().GetProperty("raycastTarget").GetValue(image);
            Assert.IsFalse(raycastTarget);

            _slot.Highlight(false);

            Assert.IsFalse(highlight.gameObject.activeSelf);
        }
    }
}
