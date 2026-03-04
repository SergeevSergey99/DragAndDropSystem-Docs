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
        [SerializeField] private List<NavigationBinding> _navigationBindings = new();
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new();

        private readonly List<PointerBinding> _resolvedPointerBindings = new();
        private readonly List<NavigationBinding> _resolvedNavigationBindings = new();
        private readonly List<InputActionBinding> _resolvedInputActionBindings = new();

        public UniversalInventory Inventory => _inventory;

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
            RebuildResolvedBindings();
            InputEventRouter.Instance.RegisterExtraBinder(this);
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.UnregisterExtraBinder(this);
        }

        private void RebuildResolvedBindings()
        {
            _resolvedPointerBindings.Clear();
            _resolvedNavigationBindings.Clear();
            _resolvedInputActionBindings.Clear();
            
            AppendValidBindings(_pointerBindings, _resolvedPointerBindings);
            AppendValidBindings(_navigationBindings, _resolvedNavigationBindings);
            AppendValidBindings(_inputActionBindings, _resolvedInputActionBindings);
            
            if (_bindingsProfile != null)
            {
                AppendValidBindings(_bindingsProfile?.PointerBindings, _resolvedPointerBindings);
                AppendValidBindings(_bindingsProfile?.NavigationBindings, _resolvedNavigationBindings);
                AppendValidBindings(_bindingsProfile?.InputActionBindings, _resolvedInputActionBindings);
            }
            
            if (_useGlobalBindingsProfile && InputEventRouter.IsInstanceExist)
            {
                AppendValidBindings(InputEventRouter.Instance.DefaultBindingsProfile?.PointerBindings, _resolvedPointerBindings);
                AppendValidBindings(InputEventRouter.Instance.DefaultBindingsProfile?.NavigationBindings, _resolvedNavigationBindings);
                AppendValidBindings(InputEventRouter.Instance.DefaultBindingsProfile?.InputActionBindings, _resolvedInputActionBindings);
            }
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
