using DragAndDropSystem.Inventories;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    [DisallowMultipleComponent]
    public class InventoryExtraInteractionBinder : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;

        [Header("Profile")]
        [SerializeField] private bool _useGlobalBindingsProfile = true;
        [SerializeField] private InteractionBindingsProfile _bindingsProfile;

        [Header("Local Bindings")]
        [SerializeField] private List<PointerBinding> _pointerBindings = new();
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new();

        private readonly List<PointerBinding> _resolvedPointerBindings = new();
        private readonly List<InputActionBinding> _resolvedInputActionBindings = new();
        // Local + profile bindings only (without the global profile).
        // Used for InputAction subscriptions; global subscriptions are handled by InputEventRouter.
        private readonly List<InputActionBinding> _localInputActionBindings = new();

        private bool _runtimeDirty = true;

        public UniversalInventory Inventory => _inventory;

        public IReadOnlyList<PointerBinding> PointerBindingsResolved
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _resolvedPointerBindings;
            }
        }

        /// <summary>
        /// Full list of InputAction bindings (local + profile + global).
        /// Used for binding resolution during event handling.
        /// </summary>
        public IReadOnlyList<InputActionBinding> InputActionBindingsResolved
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _resolvedInputActionBindings;
            }
        }

        /// <summary>
        /// Only local InputAction bindings (local + profile, without global).
        /// Used for subscriptions; global InputActions are subscribed by InputEventRouter.
        /// </summary>
        public IReadOnlyList<InputActionBinding> LocalInputActionBindings
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _localInputActionBindings;
            }
        }

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            RebuildResolvedBindings();
            InputEventRouter.AutoCreateInstance.RegisterExtraBinder(this);
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.AutoCreateInstance.UnregisterExtraBinder(this);
        }

        private void OnValidate()
        {
            _runtimeDirty = true;
        }

        private void RebuildResolvedBindings()
        {
            _resolvedPointerBindings.Clear();
            _resolvedInputActionBindings.Clear();
            _localInputActionBindings.Clear();

            AppendValidBindings(_pointerBindings, _resolvedPointerBindings);
            AppendValidBindings(_inputActionBindings, _resolvedInputActionBindings);
            AppendValidBindings(_inputActionBindings, _localInputActionBindings);

            if (_bindingsProfile != null)
            {
                AppendValidBindings(_bindingsProfile.PointerBindingsRuntime, _resolvedPointerBindings);
                AppendValidBindings(_bindingsProfile.InputActionBindingsRuntime, _resolvedInputActionBindings);
                AppendValidBindings(_bindingsProfile.InputActionBindingsRuntime, _localInputActionBindings);
            }

            if (_useGlobalBindingsProfile)
            {
                var globalProfile = InputEventRouter.AutoCreateInstance.DefaultBindingsProfile;
                if (globalProfile != null)
                {
                    AppendValidBindings(globalProfile.PointerBindingsRuntime, _resolvedPointerBindings);
                    // Global InputActions are added only to resolved (for lookup),
                    // but NOT to local (their subscriptions are handled globally by InputEventRouter).
                    AppendValidBindings(globalProfile.InputActionBindingsRuntime, _resolvedInputActionBindings);
                }
            }
            _runtimeDirty = false;
        }

        private static void AppendValidBindings<TBinding>(IReadOnlyList<TBinding> source, List<TBinding> destination)
            where TBinding : class
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    destination.Add(source[i]);
            }
        }
    }
}
