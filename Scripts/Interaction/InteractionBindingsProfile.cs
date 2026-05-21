using System.Collections.Generic;
using UnityEngine;

namespace UDND.Interaction
{
    [CreateAssetMenu(fileName = "InteractionBindingsProfile", menuName = "DragAndDrop/Interaction/Bindings Profile")]
    public sealed class InteractionBindingsProfile : ScriptableObject
    {
        [SerializeField] private List<AssetPointerBinding> _pointerBindings = new();
        [SerializeField] private List<AssetKeyBinding> _keyBindings = new();
        [SerializeField] private List<AssetLegacyInputActionBinding> _legacyInputActionBindings = new();
#if UDND_INPUT_SYSTEM
        [SerializeField] private List<AssetInputActionBinding> _inputActionBindings = new();
#endif

        private readonly List<PointerBinding> _runtimePointerBindings = new();
        private readonly List<KeyBinding> _runtimeKeyBindings = new();
        private readonly List<LegacyInputActionBinding> _runtimeLegacyInputActionBindings = new();
#if UDND_INPUT_SYSTEM
        private readonly List<InputActionBinding> _runtimeInputActionBindings = new();
#endif
        private bool _runtimeDirty = true;

        public IReadOnlyList<AssetPointerBinding> PointerBindings => _pointerBindings;
        public IReadOnlyList<AssetKeyBinding> KeyBindings => _keyBindings;
        public IReadOnlyList<AssetLegacyInputActionBinding> LegacyInputActionBindings => _legacyInputActionBindings;
#if UDND_INPUT_SYSTEM
        public IReadOnlyList<AssetInputActionBinding> InputActionBindings => _inputActionBindings;
#endif

        public IReadOnlyList<PointerBinding> PointerBindingsRuntime
        {
            get
            {
                RebuildRuntimeIfNeeded();
                return _runtimePointerBindings;
            }
        }

        public IReadOnlyList<KeyBinding> KeyBindingsRuntime
        {
            get
            {
                RebuildRuntimeIfNeeded();
                return _runtimeKeyBindings;
            }
        }

        public IReadOnlyList<LegacyInputActionBinding> LegacyInputActionBindingsRuntime
        {
            get
            {
                RebuildRuntimeIfNeeded();
                return _runtimeLegacyInputActionBindings;
            }
        }

#if UDND_INPUT_SYSTEM
        public IReadOnlyList<InputActionBinding> InputActionBindingsRuntime
        {
            get
            {
                RebuildRuntimeIfNeeded();
                return _runtimeInputActionBindings;
            }
        }
#endif

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
            _runtimeKeyBindings.Clear();
            _runtimeLegacyInputActionBindings.Clear();

            AppendRuntimeBindings(_pointerBindings, _runtimePointerBindings, b => b.ToRuntimeBinding());
            AppendRuntimeBindings(_keyBindings, _runtimeKeyBindings, b => b.ToRuntimeBinding());
            AppendRuntimeBindings(_legacyInputActionBindings, _runtimeLegacyInputActionBindings, b => b.ToRuntimeBinding());
#if UDND_INPUT_SYSTEM
            _runtimeInputActionBindings.Clear();
            AppendRuntimeBindings(_inputActionBindings, _runtimeInputActionBindings, b => b.ToRuntimeBinding());
#endif
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
