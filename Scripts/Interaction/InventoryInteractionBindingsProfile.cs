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
        [SerializeField] private List<InventoryInteractionCoordinator.PointerBinding> _pointerBindings =
            new List<InventoryInteractionCoordinator.PointerBinding>();

        [SerializeField] private bool _useNavigationBindings = true;
        [SerializeField] private List<InventoryInteractionCoordinator.NavigationBinding> _navigationBindings =
            new List<InventoryInteractionCoordinator.NavigationBinding>();

        [SerializeField] private bool _useInputActionBindings = true;
        [SerializeField] private List<InventoryInteractionCoordinator.InputActionBinding> _inputActionBindings =
            new List<InventoryInteractionCoordinator.InputActionBinding>();

        public bool UsePointerBindings => _usePointerBindings;
        public IReadOnlyList<InventoryInteractionCoordinator.PointerBinding> PointerBindings => _pointerBindings;

        public bool UseNavigationBindings => _useNavigationBindings;
        public IReadOnlyList<InventoryInteractionCoordinator.NavigationBinding> NavigationBindings => _navigationBindings;

        public bool UseInputActionBindings => _useInputActionBindings;
        public IReadOnlyList<InventoryInteractionCoordinator.InputActionBinding> InputActionBindings => _inputActionBindings;
    }
}
