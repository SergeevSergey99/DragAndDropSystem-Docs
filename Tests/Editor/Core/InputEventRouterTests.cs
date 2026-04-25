using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UniversalDragAndDrop.Interaction;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Tests.Core
{
    [TestFixture]
    public class InputEventRouterTests
    {
        private GameObject _eventSystemObject;
        private GameObject _routerObject;
        private InputEventRouter _router;
        private UniversalInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("EventSystem");
            _eventSystemObject.AddComponent<EventSystem>();

            _routerObject = new GameObject("InputEventRouter");
            _router = _routerObject.AddComponent<InputEventRouter>();

            _inventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .WithName("InputRouterInventory")
                .Build();
        }

        [TearDown]
        public void TearDown()
        {
            InventoryBuilder.Destroy(_inventory);
            if (DragAndDropManager.IsInstanceExist)
                Object.DestroyImmediate(DragAndDropManager.Instance.gameObject);
            if (_routerObject != null)
                Object.DestroyImmediate(_routerObject);
            if (_eventSystemObject != null)
                Object.DestroyImmediate(_eventSystemObject);
        }

        [Test]
        public void RoutePointerDown_DoesNotPersistMousePressedSlotAsQuickActionFocus()
        {
            var sourceAdapter = AddInputAdapter(_inventory.GetSlot(0));
            var hoveredAdapter = AddInputAdapter(_inventory.GetSlot(1));
            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };

            _router.RoutePointerEnter(sourceAdapter, eventData);
            _router.RoutePointerDown(sourceAdapter, eventData);
            _router.RoutePointerEnter(hoveredAdapter, eventData);

            Assert.AreSame(
                hoveredAdapter.BaseSlot,
                _router.ResolveQuickActionSlot(_inventory),
                "Quick actions must use the current hovered slot, not a stale mouse-down drag source.");
        }

        private static SlotInputAdapter AddInputAdapter(BaseSlot slot)
        {
            var adapter = slot.gameObject.AddComponent<SlotInputAdapter>();
            typeof(SlotInputAdapter)
                .GetField("baseSlot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(adapter, slot);
            return adapter;
        }
    }
}
