using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [CreateAssetMenu(
        fileName = "InventoryInteractionBindingsProfile",
        menuName = "DragAndDropSystem/Interaction/Bindings Profile")]
    public sealed class InventoryInteractionBindingsProfile : ScriptableObject
    {
        [SerializeField] private bool _usePointerBindings = true;
        [ShowIf(nameof(_usePointerBindings))]
        [SerializeField] private List<PointerBinding> _pointerBindings = new();

        [SerializeField] private bool _useNavigationBindings = true;
        [ShowIf(nameof(_useNavigationBindings))]
        [SerializeField] private List<NavigationBinding> _navigationBindings = new();

        [SerializeField] private bool _useInputActionBindings = true;
        [ShowIf(nameof(_useInputActionBindings))]
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new();

        public bool UsePointerBindings => _usePointerBindings;
        public IReadOnlyList<PointerBinding> PointerBindings => _pointerBindings;

        public bool UseNavigationBindings => _useNavigationBindings;
        public IReadOnlyList<NavigationBinding> NavigationBindings => _navigationBindings;

        public bool UseInputActionBindings => _useInputActionBindings;
        public IReadOnlyList<InputActionBinding> InputActionBindings => _inputActionBindings;
    }
}
