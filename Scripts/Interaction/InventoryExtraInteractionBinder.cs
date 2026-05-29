using System.Collections.Generic;
using UnityEngine;
using UDND.Inventories;

namespace UDND.Interaction
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
        [SerializeField] private List<KeyBinding> _keyBindings = new();
        [SerializeField] private List<LegacyInputActionBinding> _legacyInputActionBindings = new();
#if UDND_INPUT_SYSTEM
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new();
#endif

        private readonly List<PointerBinding> _resolvedPointerBindings = new();
        private readonly List<KeyBinding> _resolvedKeyBindings = new();
        private readonly List<LegacyInputActionBinding> _resolvedLegacyInputActionBindings = new();
#if UDND_INPUT_SYSTEM
        private readonly List<InputActionBinding> _resolvedInputActionBindings = new();
        // Local + profile bindings only (without the global profile).
        // Used for InputAction subscriptions; global subscriptions are handled by InputEventRouter.
        private readonly List<InputActionBinding> _localInputActionBindings = new();
#endif

        private bool _runtimeDirty = true;

        public IInventory Inventory => _inventory;

        public IReadOnlyList<PointerBinding> PointerBindingsResolved
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _resolvedPointerBindings;
            }
        }

        public IReadOnlyList<KeyBinding> KeyBindingsResolved
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _resolvedKeyBindings;
            }
        }

        public IReadOnlyList<LegacyInputActionBinding> LegacyInputActionBindingsResolved
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _resolvedLegacyInputActionBindings;
            }
        }

#if UDND_INPUT_SYSTEM
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
#endif

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
            _resolvedKeyBindings.Clear();
            _resolvedLegacyInputActionBindings.Clear();

            AppendValidBindings(_pointerBindings, _resolvedPointerBindings);
            AppendValidBindings(_keyBindings, _resolvedKeyBindings);
            AppendValidBindings(_legacyInputActionBindings, _resolvedLegacyInputActionBindings);

#if UDND_INPUT_SYSTEM
            _resolvedInputActionBindings.Clear();
            _localInputActionBindings.Clear();
            AppendValidBindings(_inputActionBindings, _resolvedInputActionBindings);
            AppendValidBindings(_inputActionBindings, _localInputActionBindings);
#endif

            if (_bindingsProfile != null)
            {
                AppendValidBindings(_bindingsProfile.PointerBindingsRuntime, _resolvedPointerBindings);
                AppendValidBindings(_bindingsProfile.KeyBindingsRuntime, _resolvedKeyBindings);
                AppendValidBindings(_bindingsProfile.LegacyInputActionBindingsRuntime, _resolvedLegacyInputActionBindings);
#if UDND_INPUT_SYSTEM
                AppendValidBindings(_bindingsProfile.InputActionBindingsRuntime, _resolvedInputActionBindings);
                AppendValidBindings(_bindingsProfile.InputActionBindingsRuntime, _localInputActionBindings);
#endif
            }

            if (_useGlobalBindingsProfile)
            {
                var globalProfile = InputEventRouter.AutoCreateInstance.DefaultBindingsProfile;
                if (globalProfile != null)
                {
                    AppendValidBindings(globalProfile.PointerBindingsRuntime, _resolvedPointerBindings);
                    AppendValidBindings(globalProfile.KeyBindingsRuntime, _resolvedKeyBindings);
                    AppendValidBindings(globalProfile.LegacyInputActionBindingsRuntime, _resolvedLegacyInputActionBindings);
#if UDND_INPUT_SYSTEM
                    // Global InputActions are added only to resolved (for lookup),
                    // but NOT to local (their subscriptions are handled globally by InputEventRouter).
                    AppendValidBindings(globalProfile.InputActionBindingsRuntime, _resolvedInputActionBindings);
#endif
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
