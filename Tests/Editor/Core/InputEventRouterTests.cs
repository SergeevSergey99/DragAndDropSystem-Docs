using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UDND.Interaction;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests.Core
{
    [TestFixture]
    public class InputEventRouterTests
    {
        private GameObject _eventSystemObject;
        private GameObject _routerObject;
        private InputEventRouter _router;
        private UniversalInventory _inventory;
        private UniversalInventory _secondaryInventory;

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
            InventoryBuilder.Destroy(_secondaryInventory);
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

        [Test]
        public void RuntimeState_ScopesFocusAndHoverToOwningInventory()
        {
            _secondaryInventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .WithName("InputRouterSecondaryInventory")
                .Build();

            var focusedAdapter = AddInputAdapter(_inventory.GetSlot(0));
            var hoveredAdapter = AddInputAdapter(_secondaryInventory.GetSlot(0));
            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };

            _router.RouteFocusEnter(focusedAdapter, FocusSource.Gamepad);
            _router.RoutePointerEnter(hoveredAdapter, eventData);

            Assert.AreSame(focusedAdapter.BaseSlot, _router.ResolveQuickActionSlot(_inventory, requireActiveInventory: false));
            Assert.AreSame(hoveredAdapter.BaseSlot, _router.ResolveQuickActionSlot(_secondaryInventory, requireActiveInventory: false));
            Assert.AreEqual(FocusSource.Gamepad, _router.ResolveActiveFocusSource(_inventory));
            Assert.AreEqual(FocusSource.Mouse, _router.ResolveActiveFocusSource(_secondaryInventory));
        }

        [Test]
        public void RuntimeState_KeepsPressedInventoryWhenHoverMovesToAnotherInventory()
        {
            _secondaryInventory = new InventoryBuilder()
                .WithFixedSlots(2)
                .WithName("InputRouterSecondaryInventory")
                .Build();

            var sourceAdapter = AddInputAdapter(_inventory.GetSlot(0));
            var targetAdapter = AddInputAdapter(_secondaryInventory.GetSlot(0));
            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = Vector2.zero
            };

            _router.RoutePointerDown(sourceAdapter, eventData);
            _router.RoutePointerEnter(targetAdapter, eventData);

            Assert.GreaterOrEqual(_router.GetPressedTime(_inventory), 0f);
            Assert.AreEqual(-1f, _router.GetPressedTime(_secondaryInventory));
            Assert.AreSame(targetAdapter.BaseSlot, _router.ResolveQuickActionSlot(_secondaryInventory));
        }

        [Test]
        public void LegacyInputActionBinding_TrimmedButtonName_IsValid()
        {
            var binding = new LegacyInputActionBinding(
                "Submit",
                "  Submit  ",
                ModifierKey.None,
                KeyTriggerPhase.Down,
                new TestSlotAction());

            Assert.IsTrue(binding.IsValid());
            Assert.AreEqual("Submit", binding.ButtonName);
            Assert.AreEqual(KeyTriggerPhase.Down, binding.TriggerPhase);
        }

        [Test]
        public void InteractionBindingsProfile_LegacyInputActionBinding_ConvertsToRuntimeBinding()
        {
            var profile = ScriptableObject.CreateInstance<InteractionBindingsProfile>();
            try
            {
                var assetBinding = new AssetLegacyInputActionBinding();
                SetPrivateField(assetBinding, "_label", "Submit");
                SetPrivateField(assetBinding, "_buttonName", "Submit");
                SetPrivateField(assetBinding, "_action", new TestAssetSafeSlotAction());

                SetPrivateField(
                    profile,
                    "_legacyInputActionBindings",
                    new System.Collections.Generic.List<AssetLegacyInputActionBinding> { assetBinding });

                var runtimeBindings = profile.LegacyInputActionBindingsRuntime;

                Assert.AreEqual(1, runtimeBindings.Count);
                Assert.AreEqual("Submit", runtimeBindings[0].ButtonName);
                Assert.IsTrue(runtimeBindings[0].IsValid());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static SlotInputAdapter AddInputAdapter(BaseSlot slot)
        {
            var adapter = slot.gameObject.AddComponent<SlotInputAdapter>();
            typeof(SlotInputAdapter)
                .GetField("baseSlot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(adapter, slot);
            return adapter;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private sealed class TestSlotAction : SlotInteractionAction
        {
        }

        private sealed class TestAssetSafeSlotAction : AssetSafeSlotInteractionAction
        {
        }
    }
}
