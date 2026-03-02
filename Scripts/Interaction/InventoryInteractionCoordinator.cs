using DragAndDropSystem.Inventories;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    [DisallowMultipleComponent]
    public class InventoryInteractionCoordinator : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;
        [SerializeField] private bool _logWarnings;

        [Header("Profile")]
        [SerializeField] private bool _useGlobalBindingsProfile = true;
        [SerializeField] private InventoryInteractionBindingsProfile _bindingsProfile;

        [Header("Pointer")]
        [SerializeField] private bool _usePointerBindings = true;
        [ShowIf(nameof(_usePointerBindings))]
        [SerializeField] private BindingMergeMode _pointerBindingMode = BindingMergeMode.LocalThenProfile;
        [ShowIf(nameof(_usePointerBindings))]
        [SerializeField] private List<PointerBinding> _pointerBindings = new List<PointerBinding>();

        [Header("Navigation")]
        [SerializeField] private bool _useNavigationBindings = true;
        [ShowIf(nameof(_useNavigationBindings))]
        [SerializeField] private BindingMergeMode _navigationBindingMode = BindingMergeMode.LocalThenProfile;
        [ShowIf(nameof(_useNavigationBindings))]
        [SerializeField] private List<NavigationBinding> _navigationBindings = new List<NavigationBinding>();

        [Header("Input Actions")]
        [SerializeField] private bool _useInputActionBindings = true;
        [ShowIf(nameof(_useInputActionBindings))]
        [SerializeField] private BindingMergeMode _inputActionBindingMode = BindingMergeMode.LocalThenProfile;
        [ShowIf(nameof(_useInputActionBindings))]
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new List<InputActionBinding>();

        private readonly List<PointerBinding> _resolvedPointerBindings = new List<PointerBinding>();
        private readonly List<NavigationBinding> _resolvedNavigationBindings = new List<NavigationBinding>();
        private readonly List<InputActionBinding> _resolvedInputActionBindings = new List<InputActionBinding>();

        private bool _resolvedUsePointerBindings = true;
        private bool _resolvedUseNavigationBindings = true;
        private bool _resolvedUseInputActionBindings = true;

        public UniversalInventory Inventory => _inventory;
        public bool LogWarnings => _logWarnings;

        public bool UsePointerBindingsResolved => _resolvedUsePointerBindings;
        public bool UseNavigationBindingsResolved => _resolvedUseNavigationBindings;
        public bool UseInputActionBindingsResolved => _resolvedUseInputActionBindings;

        public IReadOnlyList<PointerBinding> PointerBindingsResolved => _resolvedPointerBindings;
        public IReadOnlyList<NavigationBinding> NavigationBindingsResolved => _resolvedNavigationBindings;
        public IReadOnlyList<InputActionBinding> InputActionBindingsResolved => _resolvedInputActionBindings;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            InputEventRouter.Instance.RegisterCoordinator(this);
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.UnregisterCoordinator(this);
        }

        public void RebuildResolvedBindings(InventoryInteractionBindingsProfile globalProfile)
        {
            _resolvedPointerBindings.Clear();
            _resolvedNavigationBindings.Clear();
            _resolvedInputActionBindings.Clear();

            var profile = ResolveProfile(globalProfile);
            if (profile != null)
            {
                BuildMergedBindings(_pointerBindingMode, _usePointerBindings, _pointerBindings, profile.UsePointerBindings, profile.PointerBindings, _resolvedPointerBindings, out _resolvedUsePointerBindings);
                BuildMergedBindings(_navigationBindingMode, _useNavigationBindings, _navigationBindings, profile.UseNavigationBindings, profile.NavigationBindings, _resolvedNavigationBindings, out _resolvedUseNavigationBindings);
                BuildMergedBindings(_inputActionBindingMode, _useInputActionBindings, _inputActionBindings, profile.UseInputActionBindings, profile.InputActionBindings, _resolvedInputActionBindings, out _resolvedUseInputActionBindings);
                return;
            }

            _resolvedUsePointerBindings = _usePointerBindings;
            _resolvedUseNavigationBindings = _useNavigationBindings;
            _resolvedUseInputActionBindings = _useInputActionBindings;

            AppendValidBindings(_pointerBindings, _resolvedPointerBindings);
            AppendValidBindings(_navigationBindings, _resolvedNavigationBindings);
            AppendValidBindings(_inputActionBindings, _resolvedInputActionBindings);
        }

        private InventoryInteractionBindingsProfile ResolveProfile(InventoryInteractionBindingsProfile globalProfile)
        {
            if (_bindingsProfile != null)
                return _bindingsProfile;

            if (!_useGlobalBindingsProfile)
                return null;

            return globalProfile;
        }

        private static void BuildMergedBindings<TBinding>(
            BindingMergeMode mode,
            bool localEnabled,
            List<TBinding> localBindings,
            bool profileEnabled,
            IReadOnlyList<TBinding> profileBindings,
            List<TBinding> destination,
            out bool isEnabled)
            where TBinding : class
        {
            switch (mode)
            {
                case BindingMergeMode.LocalOnly:
                    isEnabled = localEnabled;
                    AppendValidBindings(localBindings, destination);
                    return;

                case BindingMergeMode.ProfileOnly:
                    isEnabled = profileEnabled;
                    AppendValidBindings(profileBindings, destination);
                    return;

                case BindingMergeMode.LocalThenProfile:
                    isEnabled = localEnabled || profileEnabled;
                    AppendValidBindings(localBindings, destination);
                    AppendValidBindings(profileBindings, destination);
                    return;

                default:
                    isEnabled = localEnabled;
                    AppendValidBindings(localBindings, destination);
                    return;
            }
        }

        private static void AppendValidBindings<TBinding>(IReadOnlyList<TBinding> source, List<TBinding> destination)
            where TBinding : class
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                var binding = source[i];
                if (binding != null)
                    destination.Add(binding);
            }
        }
    }
}
