using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [CreateAssetMenu(fileName = "InteractionBindingsProfile", menuName = "DragAndDrop/Interaction/Bindings Profile")]
    public sealed class InteractionBindingsProfile : ScriptableObject
    {
        [SerializeField] private List<AssetPointerBinding> _pointerBindings = new();
        [SerializeField] private List<AssetInputActionBinding> _inputActionBindings = new();

        private readonly List<PointerBinding> _runtimePointerBindings = new();
        private readonly List<InputActionBinding> _runtimeInputActionBindings = new();
        private bool _runtimeDirty = true;

        public IReadOnlyList<AssetPointerBinding> PointerBindings => _pointerBindings;
        public IReadOnlyList<AssetInputActionBinding> InputActionBindings => _inputActionBindings;

        public IReadOnlyList<PointerBinding> PointerBindingsRuntime
        {
            get
            {
                RebuildRuntimeIfNeeded();
                return _runtimePointerBindings;
            }
        }

        public IReadOnlyList<InputActionBinding> InputActionBindingsRuntime
        {
            get
            {
                RebuildRuntimeIfNeeded();
                return _runtimeInputActionBindings;
            }
        }

        private void OnEnable()
        {
            _runtimeDirty = true;
        }

        private void OnValidate()
        {
            _runtimeDirty = true;
        }

        private void RebuildRuntimeIfNeeded()
        {
            if (!_runtimeDirty)
                return;

            _runtimeDirty = false;
            _runtimePointerBindings.Clear();
            _runtimeInputActionBindings.Clear();

            AppendRuntimeBindings(_pointerBindings, _runtimePointerBindings, b => b.ToRuntimeBinding());
            AppendRuntimeBindings(_inputActionBindings, _runtimeInputActionBindings, b => b.ToRuntimeBinding());
        }

        private static void AppendRuntimeBindings<TAssetBinding, TRuntimeBinding>(
            IReadOnlyList<TAssetBinding> source,
            List<TRuntimeBinding> destination,
            System.Func<TAssetBinding, TRuntimeBinding> convert)
            where TAssetBinding : class
            where TRuntimeBinding : class
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                var binding = source[i];
                if (binding == null)
                    continue;

                var runtimeBinding = convert(binding);
                if (runtimeBinding != null)
                    destination.Add(runtimeBinding);
            }
        }
    }
}
