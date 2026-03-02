using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [CreateAssetMenu(
        fileName = "InventoryInteractionBindingsProfile",
        menuName = "DragAndDropSystem/Interaction/Bindings Profile")]
    public sealed class InventoryInteractionBindingsProfile : ScriptableObject
    {
        [SerializeField] private bool _usePointerBindings = true;
        [SerializeField] private List<PointerBinding> _pointerBindings = new();

        [SerializeField] private bool _useNavigationBindings = true;
        [SerializeField] private List<NavigationBinding> _navigationBindings = new();

        [SerializeField] private bool _useInputActionBindings = true;
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new();

        public bool UsePointerBindings => _usePointerBindings;
        public IReadOnlyList<PointerBinding> PointerBindings => _pointerBindings;

        public bool UseNavigationBindings => _useNavigationBindings;
        public IReadOnlyList<NavigationBinding> NavigationBindings => _navigationBindings;

        public bool UseInputActionBindings => _useInputActionBindings;
        public IReadOnlyList<InputActionBinding> InputActionBindings => _inputActionBindings;
    }
}
