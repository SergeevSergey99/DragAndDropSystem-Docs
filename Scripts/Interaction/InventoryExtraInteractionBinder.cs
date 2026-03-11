using DragAndDropSystem.Inventories;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public IReadOnlyList<InputActionBinding> InputActionBindingsResolved
        {
            get
            {
                if (_runtimeDirty) RebuildResolvedBindings();
                return _resolvedInputActionBindings;
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
            InputEventRouter.Instance.RegisterExtraBinder(this);
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.UnregisterExtraBinder(this);
        }

        private void OnValidate()
        {
            _runtimeDirty = true;
        }

        private void RebuildResolvedBindings()
        {
            _resolvedPointerBindings.Clear();
            _resolvedInputActionBindings.Clear();

            AppendValidBindings(_pointerBindings, _resolvedPointerBindings);
            AppendValidBindings(_inputActionBindings, _resolvedInputActionBindings);

            if (_bindingsProfile != null)
            {
                AppendValidBindings(_bindingsProfile.PointerBindingsRuntime, _resolvedPointerBindings);
                AppendValidBindings(_bindingsProfile.InputActionBindingsRuntime, _resolvedInputActionBindings);
            }

            if (_useGlobalBindingsProfile)
            {
                var globalProfile = InputEventRouter.Instance.DefaultBindingsProfile;
                if (globalProfile != null)
                {
                    AppendValidBindings(globalProfile.PointerBindingsRuntime, _resolvedPointerBindings);
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

            destination.AddRange(source.Where(binding => binding != null));
        }
    }
}
