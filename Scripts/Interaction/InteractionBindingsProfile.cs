using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [CreateAssetMenu(
        fileName = "InteractionBindingsProfile",
        menuName = "DragAndDropSystem/Interaction/Bindings Profile")]
    public sealed class InteractionBindingsProfile : ScriptableObject
    {
        [SerializeField] private List<PointerBinding> _pointerBindings = new();
        [SerializeField] private List<NavigationBinding> _navigationBindings = new();
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new();

        public IReadOnlyList<PointerBinding> PointerBindings => _pointerBindings;
        public IReadOnlyList<NavigationBinding> NavigationBindings => _navigationBindings;
        public IReadOnlyList<InputActionBinding> InputActionBindings => _inputActionBindings;
    }
}
